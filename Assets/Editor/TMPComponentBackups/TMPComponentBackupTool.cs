// TMPComponentBackupTool.cs
// Инструмент для бэкапа и восстановления компонентов TextMeshPro в Unity Editor.
// Включает ручной режим для текущей сцены/объекта и автоматический режим для всех сцен.
// Добавлена функция автоматического обновления/перерисовки TMP компонентов после восстановления.
// *** ИСПРАВЛЕНИЕ: Улучшенная обработка ссылок на ассеты (FontAsset, Material) с помощью GUID для предотвращения их потери. ***
// *** НОВОЕ: Добавлены индикаторы прогресса для операций бэкапа и восстановления. ***
// *** ИСПРАВЛЕНИЕ: Исправлена ошибка 'cannot convert from 'UnityEngine.SceneManagement.Scene' to 'UnityEngine.Object'' при использовании EditorUtility.SetDirty. ***
// ИСПРАВЛЕНИЕ: Добавлены отложенные вызовы для принудительного обновления TMP компонентов, чтобы избежать ошибки "Calling GetLocalCorners".
// Размещается в Editor/

using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using TMPro;

public class TMPComponentBackupTool : EditorWindow
{
    // Переменная для хранения пути к корневому игровому объекту для ручного восстановления
    private string _rootGameObjectPath = "";

    // Меню для открытия окна ручного бэкапа/восстановления
    [MenuItem("Tools/TMP Manual Backup/Restore Current Scene")]
    public static void ShowManualBackupWindow()
    {
        GetWindow<TMPComponentBackupTool>("TMP Manual Backup/Restore Current Scene");
    }

    // GUI для окна ручного бэкапа/восстановления
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Используйте этот инструмент для бэкапа/восстановления компонентов TMP в текущей сцене или в пределах указанного пути.", MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("📥 Создать бэкап TMP компонентов"))
            BackupAllTMPComponents(_rootGameObjectPath);

        GUILayout.Space(10);

        _rootGameObjectPath = EditorGUILayout.TextField("Путь к корневому объекту (в сцене):", _rootGameObjectPath);
        EditorGUILayout.HelpBox("Укажите полный путь к корневому игровому объекту в текущей сцене (например, 'Canvas/UI/Панель'). Если оставить пустым, будут обработаны все компоненты в текущей сцене.", MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("📤 Восстановить TMP компоненты"))
        {
            RestoreTMPComponents(_rootGameObjectPath);
        }
        GUILayout.Space(20);
        EditorGUILayout.HelpBox("Автоматический запуск для всех сцен доступен через 'Tools/TMP AutoFix All Scenes'.", MessageType.Info);
    }

    // Меню для запуска автоматической обработки всех сцен
    [MenuItem("Tools/TMP AutoFix All Scenes")]
    public static void RunAutoFixAllScenes()
    {
        string[] scenePaths = Directory.GetFiles("Assets/_Scenes", "*.unity", SearchOption.AllDirectories);

        int totalScenes = scenePaths.Length;
        int currentSceneIndex = 0;

        SceneView.RepaintAll();
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        string currentScenePath = EditorSceneManager.GetActiveScene().path;

        try // Добавляем try-finally для очистки прогресс-бара
        {
            foreach (string path in scenePaths)
            {
                currentSceneIndex++;
                EditorUtility.DisplayProgressBar("Автоматическая обработка сцен", $"Обработка сцены: {Path.GetFileName(path)} ({currentSceneIndex}/{totalScenes})", (float)currentSceneIndex / totalScenes);

                Debug.Log($"[▶] Обработка сцены: {path}");

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                BackupAllTMPComponents(""); // Прогресс будет отображаться внутри этого метода
                RestoreTMPComponents(""); // Прогресс будет отображаться внутри этого метода

                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[✔] Обработано сцен: {totalScenes}.");
        }
        finally
        {
            EditorUtility.ClearProgressBar(); // Очищаем прогресс-бар в любом случае
            // Возвращаемся к исходной сцене, если она была открыта
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath);
            }
            AssetDatabase.Refresh();
            Debug.Log("[✔] Автоматическая обработка всех сцен завершена.");
        }
    }

    // Меню для принудительного обновления всех TMP компонентов в текущей сцене
    [MenuItem("Tools/TMP Force Refresh All In Scene")]
    public static void ForceRefreshAllTMPComponentsInCurrentScene()
    {
        Debug.Log("[♻] Запуск принудительного обновления всех TMP компонентов в текущей сцене...");
        int refreshedCount = 0; // Инициализируем здесь, чтобы использовать в finally

        // Получаем все корневые объекты в текущей сцене
        List<GameObject> allGameObjects = new List<GameObject>();
        foreach (GameObject rootGo in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            allGameObjects.Add(rootGo);
            Transform[] children = rootGo.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child != rootGo.transform)
                {
                    allGameObjects.Add(child.gameObject);
                }
            }
        }

        // Предварительный подсчет компонентов для точного прогресс-бара
        List<TextMeshProUGUI> tmpComponentsToRefresh = new List<TextMeshProUGUI>();
        foreach (GameObject go in allGameObjects)
        {
            tmpComponentsToRefresh.AddRange(go.GetComponents<TextMeshProUGUI>());
        }

        int totalComponents = tmpComponentsToRefresh.Count;
        int currentComponentIndex = 0;

        try // Добавляем try-finally для очистки прогресс-бара
        {
            foreach (TextMeshProUGUI tmp in tmpComponentsToRefresh)
            {
                currentComponentIndex++;
                EditorUtility.DisplayProgressBar("Принудительное обновление TMP", $"Обновление {tmp.name} ({currentComponentIndex}/{totalComponents})", (float)currentComponentIndex / totalComponents);

                if (tmp == null) continue;

                // *** ИСПРАВЛЕНИЕ: Отложенный вызов обновления для избежания ошибки GetLocalCorners ***
                EditorApplication.delayCall += () =>
                {
                    if (tmp != null) // Проверяем, что компонент всё ещё существует после задержки
                    {
                        // Убедимся, что RectTransform валиден перед попыткой обновления
                        if (tmp.rectTransform != null && tmp.rectTransform.rect.width > 0 && tmp.rectTransform.rect.height > 0)
                        {
                            bool wasEnabled = tmp.enabled;
                            tmp.enabled = false;
                            tmp.enabled = wasEnabled;
                            EditorUtility.SetDirty(tmp); // Пометить как измененный
                                                         // refreshedCount++; // Здесь не инкрементируем, так как это отложенный вызов
                                                         // Инкремент в конце цикла или пересчет в конце
                        }
                        else
                        {
                            Debug.LogWarning($"[⚠] Пропущено отложенное обновление для {tmp.name} из-за невалидного состояния RectTransform.");
                        }
                    }
                };
                refreshedCount++; // Инкрементируем сразу, так как вызов запланирован
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Debug.Log($"[✔] Принудительное обновление завершено. Запланировано обновление {refreshedCount} TMP компонентов.");
            // ИСПРАВЛЕНО: Использование EditorSceneManager.MarkSceneDirty для сцены
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); // Маркируем сцену как измененную
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }


    // --- Вспомогательные классы для сериализации ---
    [Serializable]
    private class TMPComponentBackup
    {
        public string GameObjectPath;
        public string TypeName;
        public string AssemblyName;
        public List<string> JsonMemberKeys = new List<string>();
        public List<string> JsonMemberValues = new List<string>();
        public List<string> AssetMemberKeys = new List<string>();
        public List<string> AssetMemberGUIDs = new List<string>();
    }

    [Serializable]
    private class TMPBackupWrapper
    {
        public List<TMPComponentBackup> Backups = new List<TMPComponentBackup>();
    }

    // --- Общие вспомогательные методы ---
    private static string SceneNameSafe => SceneManager.GetActiveScene().name.Replace(" ", "_");
    private static string BackupFolder => Path.Combine("Assets", "TMPComponentBackups");

    private static string GetBackupFilePath(string rootPath)
    {
        string baseFileName;
        if (!string.IsNullOrEmpty(rootPath))
        {
            string sanitizedRootPath = rootPath.Replace("/", "_").Replace("\\", "_");
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitizedRootPath = sanitizedRootPath.Replace(c.ToString(), "");
            }
            baseFileName = $"{SceneNameSafe}_{sanitizedRootPath}_backup.json";
        }
        else
        {
            baseFileName = $"{SceneNameSafe}_full_scene_backup.json";
        }
        return Path.Combine(BackupFolder, baseFileName);
    }

    private static bool IsObsolete(MemberInfo member) => member.IsDefined(typeof(ObsoleteAttribute), true);

    private static bool IsJsonSerializable(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            return true;

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return false;

        if (type.IsArray && type.GetElementType() != null && IsJsonSerializable(type.GetElementType()))
            return true;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return type.GetGenericArguments().Length > 0 && IsJsonSerializable(type.GetGenericArguments()[0]);
        }

        if (type.IsClass || type.IsValueType)
        {
            return type.IsDefined(typeof(SerializableAttribute), false);
        }

        return false;
    }

    private static bool IsTMPRelated(Type type)
    {
        if (type == null) return false;
        while (type != null)
        {
            if (type.FullName.Contains("TextMeshPro") || type.FullName.Contains("TMPro")) return true;
            type = type.BaseType;
        }
        return false;
    }

    private static string GetFullPath(Transform t)
    {
        if (t.parent == null) return t.name;
        return GetFullPath(t.parent) + "/" + t.name;
    }

    // --- Основные методы бэкапа и восстановления ---

    // Публичный статический метод для создания бэкапа
    public static void BackupAllTMPComponents(string rootPath)
    {
        TMPBackupWrapper wrapper = new TMPBackupWrapper();
        List<GameObject> allGameObjects = new List<GameObject>();
        foreach (GameObject rootGo in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            allGameObjects.Add(rootGo);
            Transform[] children = rootGo.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child != rootGo.transform)
                {
                    allGameObjects.Add(child.gameObject);
                }
            }
        }

        // Предварительно фильтруем TMP компоненты для подсчета общего количества
        List<MonoBehaviour> tmpComponentsToBackup = new List<MonoBehaviour>();
        foreach (GameObject go in allGameObjects)
        {
            MonoBehaviour[] componentsOnGo = go.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour comp in componentsOnGo)
            {
                if (comp == null || !IsTMPRelated(comp.GetType())) continue;
                tmpComponentsToBackup.Add(comp);
            }
        }

        int totalComponents = tmpComponentsToBackup.Count;
        int currentComponentIndex = 0;

        try // Добавляем try-finally для очистки прогресс-бара
        {
            foreach (MonoBehaviour comp in tmpComponentsToBackup)
            {
                currentComponentIndex++;
                EditorUtility.DisplayProgressBar("Создание бэкапа TMP компонентов", $"Бэкап {comp.GetType().Name} на '{GetFullPath(comp.transform)}' ({currentComponentIndex}/{totalComponents})", (float)currentComponentIndex / totalComponents);

                TMPComponentBackup backup = new TMPComponentBackup
                {
                    GameObjectPath = GetFullPath(comp.transform),
                    TypeName = comp.GetType().FullName,
                    AssemblyName = comp.GetType().Assembly.GetName().Name
                };

                // Backup Fields
                FieldInfo[] fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    if (IsObsolete(field) || field.IsNotSerialized) continue;

                    try
                    {
                        if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                        {
                            UnityEngine.Object assetValue = field.GetValue(comp) as UnityEngine.Object;
                            string guid = "";
                            if (assetValue != null)
                            {
                                string assetPath = AssetDatabase.GetAssetPath(assetValue);
                                guid = AssetDatabase.AssetPathToGUID(assetPath);
                            }
                            backup.AssetMemberKeys.Add(field.Name);
                            backup.AssetMemberGUIDs.Add(guid);
                        }
                        else if (IsJsonSerializable(field.FieldType))
                        {
                            object value = field.GetValue(comp);
                            string jsonValue = (value == null) ? "null" : JsonUtility.ToJson(value);
                            backup.JsonMemberKeys.Add(field.Name);
                            backup.JsonMemberValues.Add(jsonValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[⚠] Не удалось забэкапить поле '{field.Name}' на {comp.name}: {ex.Message}");
                    }
                }

                // Backup Properties
                PropertyInfo[] props = comp.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (PropertyInfo prop in props)
                {
                    if (IsObsolete(prop) || !prop.CanRead) continue;
                    if (prop.Name.Equals("tag", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        if (typeof(UnityEngine.Object).IsAssignableFrom(prop.PropertyType))
                        {
                            UnityEngine.Object assetValue = prop.GetValue(comp) as UnityEngine.Object;
                            string guid = "";
                            if (assetValue != null)
                            {
                                string assetPath = AssetDatabase.GetAssetPath(assetValue);
                                guid = AssetDatabase.AssetPathToGUID(assetPath);
                            }
                            backup.AssetMemberKeys.Add(prop.Name);
                            backup.AssetMemberGUIDs.Add(guid);
                        }
                        else if (IsJsonSerializable(prop.PropertyType))
                        {
                            object value = prop.GetValue(comp);
                            string jsonValue = (value == null) ? "null" : JsonUtility.ToJson(value);
                            backup.JsonMemberKeys.Add(prop.Name);
                            backup.JsonMemberValues.Add(jsonValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[⚠] Не удалось забэкапить свойство '{prop.Name}' на {comp.name}: {ex.Message}");
                    }
                }

                wrapper.Backups.Add(backup);
            }

            if (!Directory.Exists(BackupFolder))
            {
                Directory.CreateDirectory(BackupFolder);
            }

            string finalBackupFilePath = GetBackupFilePath(rootPath);
            string jsonOutput = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(finalBackupFilePath, jsonOutput);
            Debug.Log($"[✔] Бэкап TMP компонентов создан по пути: {finalBackupFilePath}. Всего скопировано: {wrapper.Backups.Count} компонента(ов).");
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar(); // Очищаем прогресс-бар в любом случае
        }
    }

    // Публичный статический метод для восстановления
    public static void RestoreTMPComponents(string rootPath)
    {
        string finalBackupFilePath = GetBackupFilePath(rootPath);
        if (!File.Exists(finalBackupFilePath))
        {
            Debug.LogError($"[✖] Файл бэкапа не найден по пути: {finalBackupFilePath}");
            return;
        }

        string jsonContent = File.ReadAllText(finalBackupFilePath);

        TMPBackupWrapper _wrapper = JsonUtility.FromJson<TMPBackupWrapper>(jsonContent);

        if (_wrapper == null || _wrapper.Backups == null || _wrapper.Backups.Count == 0)
        {
            Debug.LogWarning("[⚠] Файл бэкапа пуст или некорректен.");
            return;
        }

        int totalBackups = _wrapper.Backups.Count;
        int currentBackupIndex = 0;

        try // Добавляем try-finally для очистки прогресс-бара
        {
            foreach (TMPComponentBackup backup in _wrapper.Backups)
            {
                currentBackupIndex++;
                EditorUtility.DisplayProgressBar("Восстановление TMP компонентов", $"Восстановление '{backup.TypeName}' на '{backup.GameObjectPath}' ({currentBackupIndex}/{totalBackups})", (float)currentBackupIndex / totalBackups);

                if (!string.IsNullOrEmpty(rootPath))
                {
                    string normalizedRootPath = rootPath.TrimEnd('/');
                    if (!backup.GameObjectPath.Equals(normalizedRootPath, StringComparison.OrdinalIgnoreCase) &&
                        !backup.GameObjectPath.StartsWith(normalizedRootPath + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[~] Пропускаем компонент '{backup.TypeName}' на '{backup.GameObjectPath}', так как он не находится под '{rootPath}'.");
                        continue;
                    }
                }

                GameObject go = GameObject.Find(backup.GameObjectPath);
                if (go == null)
                {
                    Debug.LogWarning($"[!] Не найден GameObject по пути: {backup.GameObjectPath}. Пропускаем восстановление для этого объекта.");
                    continue;
                }

                Type type = Type.GetType($"{backup.TypeName}, {backup.AssemblyName}");
                if (type == null)
                {
                    type = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(a => a.GetTypes())
                                .FirstOrDefault(t => t.FullName == backup.TypeName);

                    if (type == null)
                    {
                        Debug.LogError($"[✖] Тип не найден: {backup.TypeName} из {backup.AssemblyName} (или без указания сборки). Пропускаем компонент.");
                        continue;
                    }
                    else
                    {
                        Debug.LogWarning($"[⚠] Тип '{backup.TypeName}' найден, но с другим AssemblyName или без него. Восстановление может быть неполным.");
                    }
                }

                Component comp = go.GetComponent(type);
                if (comp == null)
                {
                    comp = go.AddComponent(type);
                    Debug.Log($"[+] Добавлен новый компонент '{type.Name}' на GameObject '{go.name}'.");
                }
                else
                {
                    Debug.Log($"[✓] Компонент '{type.Name}' уже существует на GameObject '{go.name}'.");
                }

                if (comp == null)
                {
                    Debug.LogError($"[✖] Не удалось получить или добавить компонент '{type.Name}' на GameObject '{go.name}'.");
                    continue;
                }

                // --- Восстановление JSON-сериализуемых членов ---
                for (int i = 0; i < backup.JsonMemberKeys.Count; i++)
                {
                    string memberName = backup.JsonMemberKeys[i];
                    string memberJson = backup.JsonMemberValues[i];

                    try
                    {
                        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (prop != null && prop.Name.Equals("tag", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        Type memberType = field != null ? field.FieldType : prop.PropertyType;
                        object objToSet = (memberJson == "null") ? null : JsonUtility.FromJson(memberJson, memberType);

                        if (field != null)
                        {
                            field.SetValue(comp, objToSet);
                        }
                        else if (prop != null && prop.CanWrite)
                        {
                            prop.SetValue(comp, objToSet, null);
                        }
                        else
                        {
                            Debug.LogWarning($"[⚠] Не удалось найти или записать JSON-член '{memberName}' на '{comp.GetType().Name}'. Возможно, он был переименован или удален.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[⚠] Не удалось восстановить JSON-член '{memberName}' на {go.name}: {ex.Message}");
                    }
                }

                // --- Восстановление ссылок на ассеты по GUID ---
                for (int i = 0; i < backup.AssetMemberKeys.Count; i++)
                {
                    string memberName = backup.AssetMemberKeys[i];
                    string assetGuid = backup.AssetMemberGUIDs[i];

                    try
                    {
                        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        Type memberType = field != null ? field.FieldType : prop.PropertyType;
                        UnityEngine.Object loadedAsset = null;

                        if (!string.IsNullOrEmpty(assetGuid))
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                loadedAsset = AssetDatabase.LoadAssetAtPath(assetPath, memberType);
                            }
                            else
                            {
                                Debug.LogWarning($"[⚠] Ассет с GUID '{assetGuid}' для члена '{memberName}' не найден по пути. Возможно, он был перемещен или удален.");
                            }
                        }

                        if (field != null)
                        {
                            field.SetValue(comp, loadedAsset);
                        }
                        else if (prop != null && prop.CanWrite)
                        {
                            prop.SetValue(comp, loadedAsset, null);
                        }
                        else
                        {
                            Debug.LogWarning($"[⚠] Не удалось найти или записать ассет-член '{memberName}' на '{comp.GetType().Name}'. Возможно, он был переименован или удален.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[⚠] Не удалось восстановить ассет-член '{memberName}' на {go.name}: {ex.Message}");
                    }
                }

                // *** ИСПРАВЛЕНИЕ: Отложенный вызов принудительного обновления TMP компонента после восстановления ***
                if (comp is TextMeshProUGUI tmpComponent)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (tmpComponent != null) // Проверяем, что компонент всё ещё существует после задержки
                        {
                            // Убедимся, что RectTransform валиден перед попыткой обновления
                            if (tmpComponent.rectTransform != null && tmpComponent.rectTransform.rect.width > 0 && tmpComponent.rectTransform.rect.height > 0)
                            {
                                bool wasEnabled = tmpComponent.enabled;
                                tmpComponent.enabled = false;
                                tmpComponent.enabled = wasEnabled;
                                EditorUtility.SetDirty(tmpComponent); // Пометить как измененный
                            }
                            else
                            {
                                Debug.LogWarning($"[⚠] Пропущено отложенное обновление для {tmpComponent.name} из-за невалидного состояния RectTransform после восстановления.");
                            }
                        }
                    };
                }
            }

            Debug.Log("[✔] Восстановление TMP компонентов завершено. Отложенные обновления запланированы.");
            // ИСПРАВЛЕНО: Использование EditorSceneManager.MarkSceneDirty для сцены
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); // Маркируем сцену как измененную
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar(); // Очищаем прогресс-бар в любом случае
        }
    }
}