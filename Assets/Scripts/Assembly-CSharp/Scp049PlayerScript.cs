using Dissonance.Integrations.UNet_HLAPI; // Если Dissonance HLAPI все еще используется
using Mirror;
using RemoteAdmin; // Возможно, используется для классов или утилит, но UnityEngine.Networking.NetworkClient - устарело
using UnityEngine;
using UnityEngine.UI;
using System; // Для Math.Abs или Mathf.Approximately, если они нужны

// Убедитесь, что этот скрипт находится на игровом объекте с NetworkIdentity
public class Scp049PlayerScript : NetworkBehaviour
{
    [Header("Player Properties")]
    public GameObject PlayerCameraGameObject;

    public bool IsScp049;

    public bool IsSameClassAsScp;

    public GameObject ScpInstance;

    [Header("Infection")]
    // Текущий прогресс заражения игрока (уменьшается со временем)
    public float CurrentInfectionProgress;

    [Header("Attack & Recall")]
    // Максимальная дистанция для обычной атаки
    public float AttackDistance = 2.4f;

    // Максимальная дистанция для начала реанимации (Recall)
    public float RecallDistance = 3.5f;

    // Прогресс реанимации трупа
    public float RecallProgress;

    // Количество вылеченных (реанимированных) игроков
    public int CuredPlayersCount;

    // ====================================================================================
    // --- ПРИВАТНЫЕ ПОЛЯ (ВНУТРЕННИЕ СОСТОЯНИЯ) ---
    // ====================================================================================

    // Объект (игрок), который в данный момент реанимируется (на стороне клиента)
    private GameObject _recallingPlayerObjectClient;

    // Ragdoll объект, который в данный момент реанимируется (на стороне клиента)
    private Ragdoll _recallingRagdollClient;

    // Ссылка на интерфейсы SCP (предположительно UI элементы)
    private ScpInterfaces _scpInterfaces;

    // UI элемент для отображения прогресса реанимации
    private Image _loadingCircleUI;

    // Ссылка на компонент FirstPersonController
    private FirstPersonController _firstPersonController;

    // ====================================================================================
    // --- НАСТРОЙКИ БУСТОВ (АНИМАЦИОННЫЕ КРИВЫЕ) ---
    // ====================================================================================

    [Header("Boosts")]
    // Кривая, определяющая время реанимации в зависимости от здоровья SCP-049
    public AnimationCurve BoostRecallTimeCurve;

    // Кривая, определяющая время заражения в зависимости от здоровья SCP-049
    public AnimationCurve BoostInfectTimeCurve;

    // ====================================================================================
    // --- СЕРВЕРНЫЕ СОСТОЯНИЯ ДЛЯ РЕАНИМАЦИИ (PRIVATE SYNCVARS НЕ НУЖНЫ, ЭТО СЕРВЕРНЫЕ ВНУТРЕННИЕ) ---
    // ====================================================================================

    // Объект игрока, который реанимируется на сервере
    private GameObject _serverRecallingObject;

    // Прогресс реанимации на сервере
    private float _serverRecallProgress;

    // Флаг, указывающий, идет ли процесс реанимации на сервере
    private bool _isServerRecallInProgress;

    // ====================================================================================
    // --- МЕТОДЫ UNITY LIFECYCLE ---
    // ====================================================================================

    private void Start()
    {
        // Поиск и кэширование ссылки на ScpInterfaces
        _scpInterfaces = FindObjectOfType<ScpInterfaces>();
        if (_scpInterfaces != null)
        {
            _loadingCircleUI = _scpInterfaces.Scp049_loading;
        }
        else
        {
            Debug.LogWarning("Scp049PlayerScript: ScpInterfaces component not found in scene!");
        }

        // Кэширование FirstPersonController только для локального игрока
        if (isLocalPlayer)
        {
            _firstPersonController = GetComponent<FirstPersonController>();
        }
    }

    private void Update()
    {
        DeductInfectionProgress(); // Уменьшаем прогресс заражения
        HandleInput(); // Обрабатываем ввод игрока
        UpdateServerRecallProgress(); // Обновляем серверный прогресс реанимации
    }

    // ====================================================================================
    // --- ИНИЦИАЛИЗАЦИЯ И ОБНОВЛЕНИЕ СОСТОЯНИЯ ---
    // ====================================================================================

    // Метод инициализации класса игрока
    public void Init(int classID, Class c)
    {
        IsSameClassAsScp = c.team == Team.SCP;
        IsScp049 = classID == 5;

        // Активация UI только для локального игрока, если он SCP-049
        if (isLocalPlayer && _scpInterfaces != null)
        {
            _scpInterfaces.Scp049_eq.SetActive(IsScp049);
        }
    }

    // Уменьшение прогресса заражения со временем
    private void DeductInfectionProgress()
    {
        if (CurrentInfectionProgress > 0f)
        {
            CurrentInfectionProgress -= Time.deltaTime;
        }
        else if (CurrentInfectionProgress < 0f)
        {
            CurrentInfectionProgress = 0f;
        }
    }

    // Обновление прогресса реанимации на сервере (если активна)
    private void UpdateServerRecallProgress()
    {
        if (_isServerRecallInProgress)
        {
            // Увеличиваем прогресс, используя кривую скорости реанимации
            _serverRecallProgress += Time.deltaTime / BoostRecallTimeCurve.Evaluate(GetComponent<PlayerStats>().GetHealthPercent());

            // Если прогресс достиг или превысил 2 секунды (порог завершения)
            if (_serverRecallProgress >= 2f)
            {
                _isServerRecallInProgress = false;
                _serverRecallProgress = 0f;
                _serverRecallingObject = null;
            }
        }
    }

    // ====================================================================================
    // --- ОБРАБОТКА ВВОДА ИГРОКА ---
    // ====================================================================================

    // Обработка ввода (атака, реанимация)
    private void HandleInput()
    {
        if (!isLocalPlayer)
        {
            return; // Только локальный игрок может обрабатывать ввод
        }

        if (Input.GetKeyDown(NewInput.GetKey("Shoot")))
        {
            PerformAttack();
        }
        if (Input.GetKeyDown(NewInput.GetKey("Interact")))
        {
            StartSurgery(); // Начинаем или продолжаем реанимацию
        }
        UpdateRecallingState(); // Обновляем состояние процесса реанимации (клиентская часть)
    }

    // Логика атаки SCP-049
    private void PerformAttack()
    {
        // Проверяем, является ли игрок SCP-049 и есть ли цель в поле зрения
        if (IsScp049 && Physics.Raycast(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward, out RaycastHit hitInfo, AttackDistance))
        {
            // Пытаемся получить компонент Scp049PlayerScript с пораженного объекта
            Scp049PlayerScript targetScp049Script = hitInfo.transform.GetComponent<Scp049PlayerScript>();

            // Если компонент найден и цель не является союзником SCP-049
            if (targetScp049Script != null && !targetScp049Script.IsSameClassAsScp)
            {
                // Отправляем команду на сервер для заражения игрока
                CmdInfectPlayer(targetScp049Script.gameObject, GetComponent<HlapiPlayer>().PlayerId);
            }
        }
    }

    // Логика начала реанимации (Surgery)
    private void StartSurgery()
    {
        // Проверяем, является ли игрок SCP-049 и есть ли труп в поле зрения
        if (!IsScp049 || !Physics.Raycast(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward, out RaycastHit hitInfo, RecallDistance))
        {
            return;
        }

        // Пытаемся получить компонент Ragdoll с пораженного объекта или его родителя
        Ragdoll hitRagdoll = hitInfo.transform.GetComponentInParent<Ragdoll>();

        // Если это не Ragdoll или реанимация не разрешена для этого Ragdoll
        if (hitRagdoll == null || !hitRagdoll.allowRecall)
        {
            return;
        }

        // Ищем соответствующего игрока (владельца Ragdoll) среди всех игроков
        GameObject[] allPlayers = PlayerManager.singleton.players;
        foreach (GameObject player in allPlayers)
        {
            HlapiPlayer hlapiPlayer = player.GetComponent<HlapiPlayer>();
            Scp049PlayerScript targetScp049Script = player.GetComponent<Scp049PlayerScript>();

            // Если это владелец Ragdoll, и он заражен, и Ragdoll разрешает реанимацию
            if (hlapiPlayer != null && hlapiPlayer.PlayerId == hitRagdoll.owner.ownerHLAPI_id &&
                targetScp049Script != null && targetScp049Script.CurrentInfectionProgress > 0f && hitRagdoll.allowRecall)
            {
                _recallingPlayerObjectClient = player;
                _recallingRagdollClient = hitRagdoll;
                // Отправляем команду на сервер для начала реанимации
                CmdStartInfecting(_recallingPlayerObjectClient, _recallingRagdollClient.gameObject);
                return; // Нашли подходящий труп, выходим
            }
        }
    }

    // Обновление состояния процесса реанимации (клиентская часть)
    private void UpdateRecallingState()
    {
        // Если SCP-049 и кнопка "Interact" зажата, и есть объект для реанимации
        if (IsScp049 && Input.GetKey(NewInput.GetKey("Interact")) && _recallingPlayerObjectClient != null)
        {
            if (_firstPersonController != null)
            {
                _firstPersonController.lookingAtMe = true; // Визуальный индикатор (если нужен)
            }

            // Увеличиваем прогресс реанимации
            RecallProgress += Time.deltaTime / BoostRecallTimeCurve.Evaluate(GetComponent<PlayerStats>().GetHealthPercent());

            // Если прогресс достиг 100%
            if (RecallProgress >= 1f)
            {
                CuredPlayersCount++; // Увеличиваем счетчик реанимированных
                if (CuredPlayersCount > 9)
                {
                    AchievementManager.Achieve("turnthemall"); // Достижение
                }

                // Отправляем команду на сервер для завершения реанимации
                CmdRecallPlayer(_recallingPlayerObjectClient, _recallingRagdollClient.gameObject);
                ResetRecallStateClient(); // Сбрасываем клиентское состояние
            }
        }
        else // Если кнопка отпущена или нет объекта для реанимации
        {
            if (_recallingPlayerObjectClient != null)
            {
                CmdAbortInfecting(); // Отправляем команду на сервер для отмены
                ResetRecallStateClient(); // Сбрасываем клиентское состояние
            }
            if (IsScp049 && _firstPersonController != null)
            {
                _firstPersonController.lookingAtMe = false; // Сбрасываем визуальный индикатор
            }
        }

        // Обновляем UI прогресса реанимации
        if (_loadingCircleUI != null)
        {
            _loadingCircleUI.fillAmount = RecallProgress;
        }
    }

    // Сброс клиентских переменных состояния реанимации
    private void ResetRecallStateClient()
    {
        _recallingPlayerObjectClient = null;
        _recallingRagdollClient = null;
        RecallProgress = 0f;
    }

    // Наносит удар и отправляет команду на сервер для заражения
    private void InfectPlayer(GameObject target, string id)
    {
        CmdInfectPlayer(target, id);
        Hitmarker.Hit(); // Активируем маркер попадания на клиенте
    }

    // ====================================================================================
    // --- COMMANDS (ВЫЗЫВАЮТСЯ НА СЕРВЕРЕ) ---
    // ====================================================================================

    // Команда: Клиент просит сервер начать процесс реанимации
    [Command(channel = 2)] // Используем канал 2 (предполагается, что он Reliable)
    public void CmdStartInfecting(GameObject targetPlayer, GameObject targetRagdoll)
    {
        if (targetPlayer == null || targetRagdoll == null)
        {
            Debug.LogWarning("CmdStartInfecting: Target player or ragdoll is null.");
            return;
        }

        Ragdoll ragdollComponent = targetRagdoll.GetComponent<Ragdoll>();
        QueryProcessor targetQueryProcessor = targetPlayer.GetComponent<QueryProcessor>();
        PlyMovementSync playerMovementSync = GetComponent<PlyMovementSync>(); // Получаем доступ к текущей позиции SCP-049

        // Серверные проверки для валидации запроса
        if (ragdollComponent == null || !ragdollComponent.allowRecall ||
            targetQueryProcessor == null || ragdollComponent.owner.PlayerId != targetQueryProcessor.PlayerId ||
            !IsScp049 ||
            playerMovementSync == null || Vector3.Distance(targetRagdoll.transform.position, playerMovementSync.CurrentPosition) >= AttackDistance * 1.3f)
        {
            Debug.LogWarning("CmdStartInfecting: Validation failed for starting recall.");
            return;
        }

        // Запускаем процесс реанимации на сервере
        _serverRecallingObject = targetPlayer;
        _serverRecallProgress = 0f;
        _isServerRecallInProgress = true;
    }

    // Команда: Клиент просит сервер отменить процесс реанимации
    [Command(channel = 2)]
    public void CmdAbortInfecting()
    {
        // Сбрасываем серверное состояние реанимации
        _isServerRecallInProgress = false;
        _serverRecallingObject = null;
        _serverRecallProgress = 0f;
    }

    // Команда: Клиент просит сервер заразить игрока
    [Command(channel = 2)]
    private void CmdInfectPlayer(GameObject targetPlayer, string instigatorId) // Имя instigatorId более осмысленно
    {
        PlyMovementSync playerMovementSync = GetComponent<PlyMovementSync>(); // Позиция атакующего SCP-049

        // Серверные проверки для валидации атаки
        if (!IsScp049 || targetPlayer == null || playerMovementSync == null ||
            Vector3.Distance(targetPlayer.transform.position, playerMovementSync.CurrentPosition) >= AttackDistance * 1.3f)
        {
            Debug.LogWarning("CmdInfectPlayer: Validation failed for infecting player.");
            return;
        }

        PlayerStats targetStats = targetPlayer.GetComponent<PlayerStats>();
        NicknameSync attackerNickname = GetComponent<NicknameSync>();
        CharacterClassManager attackerClassManager = GetComponent<CharacterClassManager>();

        if (targetStats != null && attackerNickname != null && attackerClassManager != null)
        {
            // Наносим урон цели
            targetStats.HurtPlayer(new PlayerStats.HitInfo(
                4949f,
                $"{attackerNickname.myNick} ({attackerClassManager.SteamId})",
                DamageTypes.Scp049,
                GetComponent<QueryProcessor>().PlayerId), // ID атакующего SCP-049
                targetPlayer);

            // Отправляем Rpc на всех клиентов для обновления статуса заражения цели
            RpcInfectPlayer(targetPlayer, BoostInfectTimeCurve.Evaluate(GetComponent<PlayerStats>().GetHealthPercent()));
        }
    }

    // Команда: Клиент просит сервер завершить реанимацию
    [Command(channel = 2)]
    private void CmdRecallPlayer(GameObject targetPlayer, GameObject targetRagdoll)
    {
        // Дополнительные серверные проверки для предотвращения читерства
        if (!_isServerRecallInProgress || targetPlayer != _serverRecallingObject || _serverRecallProgress < 0.85f) // Прогресс должен быть достаточно высоким
        {
            Debug.LogWarning("CmdRecallPlayer: Server recall validation failed or progress too low.");
            return;
        }

        CharacterClassManager targetClassManager = targetPlayer.GetComponent<CharacterClassManager>();
        Ragdoll ragdollComponent = targetRagdoll.GetComponent<Ragdoll>();

        // Проверки на сервере: класс игрока, SCP-049 ли это, причина смерти
        if (ragdollComponent == null || targetClassManager == null ||
            targetClassManager.curClass != 2 || !IsScp049 ||
            ragdollComponent.owner.deathCause.GetDamageType() != DamageTypes.Scp049)
        {
            Debug.LogWarning("CmdRecallPlayer: Final validation failed for recalling player.");
            return;
        }

        RoundSummary.changed_into_zombies++; // Обновляем статистику раунда
        targetClassManager.SetClassID(10); // Устанавливаем новый класс (например, зомби)
        targetPlayer.GetComponent<PlayerStats>().Health = targetClassManager.klasy[10].maxHP; // Восстанавливаем здоровье

        // Уничтожаем Ragdoll объект на всех клиентах через NetworkServer.Destroy
        DestroyPlayerRagdoll(targetRagdoll);

        // Сбрасываем серверное состояние реанимации
        _isServerRecallInProgress = false;
        _serverRecallingObject = null;
        _serverRecallProgress = 0f;
    }

    // ====================================================================================
    // --- CLIENTRPC (ВЫЗЫВАЮТСЯ НА КЛИЕНТАХ) ---
    // ====================================================================================

    // Rpc: Сервер говорит всем клиентам обновить прогресс заражения цели
    [ClientRpc(channel = 2)]
    private void RpcInfectPlayer(GameObject targetPlayer, float infectionTime)
    {
        Scp049PlayerScript targetScp049Script = targetPlayer.GetComponent<Scp049PlayerScript>();
        if (targetScp049Script != null)
        {
            targetScp049Script.CurrentInfectionProgress = infectionTime;
        }
    }

    // ====================================================================================
    // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ СЕРВЕРА ---
    // ====================================================================================

    // Серверный метод для уничтожения Ragdoll объекта
    [Server] // Убеждаемся, что этот метод вызывается только на сервере
    private void DestroyPlayerRagdoll(GameObject ragdollToDestroy)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] DestroyPlayerRagdoll: Called on client!");
            return;
        }

        if (ragdollToDestroy != null && ragdollToDestroy.CompareTag("Ragdoll"))
        {
            NetworkServer.Destroy(ragdollToDestroy);
        }
    }
}