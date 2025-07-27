using AntiFaker;
using MEC; // Assuming MEC (More Effective Coroutines) is still used
using Mirror; // Using Mirror namespace
using System;
using System.Collections.Generic;
using UnityEngine;

// Убедитесь, что этот скрипт находится на игровом объекте с NetworkIdentity
public class PlyMovementSync : NetworkBehaviour
{
    // ====================================================================================
    // --- ПУБЛИЧНЫЕ ПОЛЯ И СВОЙСТВА ---
    // ====================================================================================

    // Текущая позиция, синхронизированная с сервером
    public Vector3 CurrentPosition;

    // Текущее вращение по оси Y, синхронизированное с сервером
    public float CurrentRotationY;

    // Вращение камеры по оси X (вертикальное), синхронизированное через SyncVar
    [SyncVar(hook = nameof(OnRotXChanged))] // Используем hook для применения изменения сразу
    public float rotX;

    // Используется для получения значения SyncVar в других скриптах, если необходимо
    // В данном случае, Mirror напрямую управляет rotX через SyncVar.
    // Если вам нужно было бы выполнять дополнительную логику при изменении,
    // можно было бы использовать эту property с дополнительными действиями.
    public float NetworkRotX
    {
        get => rotX;
        set => rotX = value; // Mirror сам вызовет хук OnRotXChanged при изменении SyncVar
    }

    // ====================================================================================
    // --- ПРИВАТНЫЕ ПОЛЯ ---
    // ====================================================================================

    // Последние полученные или отправленные данные для проверки изменений
    private Vector3 _lastSyncedPos;
    private float _lastSyncedRotY;
    private float _lastSyncedRotX; // Для вращения камеры X

    // Приватное поле для хранения текущего вращения игрока (используется только локально)
    private float _localPlayerRotationY;

    // Ссылка на компонент CharacterClassManager
    [HideInInspector]
    public CharacterClassManager characterClassManager;

    // Ссылки на другие необходимые компоненты
    private AntiFakeCommands _antiFakeCommands;
    private Scp106PlayerScript _scp106Script;
    private Transform _playerCameraTransform; // Кэшированная ссылка на transform камеры
    private FootstepSync _footstepSync; // Если этот компонент действительно используется
    private FallDamage _fallDamage;

    // Флаги состояния
    public bool IsGrounded;
    private bool _isInputAllowed;
    private bool _unstuckNeeded;
    private bool _wasUsingPortal;

    // Переменные для отслеживания полета/падения
    public float FlyTime;
    public float GroundedYPosition; // Y-координата последней точки приземления

    // Для телепортации/перемещения игрока сервером
    private Vector3 _serverTeleportTargetPosition;

    // ====================================================================================
    // --- МЕТОДЫ UNITY LIFECYCLE ---
    // ====================================================================================

    private void Start()
    {
        // Кэширование ссылок на компоненты
        _playerCameraTransform = GetComponent<Scp049PlayerScript>()?.PlayerCameraGameObject.transform;
        _antiFakeCommands = GetComponent<AntiFakeCommands>();
        characterClassManager = GetComponent<CharacterClassManager>();
        _scp106Script = GetComponent<Scp106PlayerScript>();
        _fallDamage = GetComponent<FallDamage>();

        FlyTime = 0f;
        _serverTeleportTargetPosition = Vector3.zero;
        _isInputAllowed = true;

        // Запускаем корутину проверки застревания только на сервере и в начале раунда
        if (isServer && RoundStart.RoundJustStarted)
        {
            Timing.RunCoroutine(UnstuckCheckRoutine(), Segment.Update);
        }
    }

    private void FixedUpdate()
    {
        // Только локальный игрок отправляет свои данные на сервер
        if (isLocalPlayer)
        {
            _localPlayerRotationY = transform.rotation.eulerAngles.y;
            TransmitMovementData();
        }
    }

    [ClientCallback]
    private void TransmitMovementData()
    {
        if (isLocalPlayer)
        {
            CmdSyncData(_localPlayerRotationY, transform.position, GetComponent<PlayerInteract>().playerCamera.transform.localRotation.eulerAngles.x);
        }
    }

    // Команда, отправляемая клиентом на сервер
    [Command(channel = 5)] // Убедитесь, что канал 5 настроен как Unreliable/UnreliableSequenced в LiteNetLib4MirrorTransport
    private void CmdSyncData(float clientRotY, Vector3 clientPos, float clientRotX)
    {
        // Проверяем, изменились ли данные существенно, чтобы избежать лишней обработки
        if (Mathf.Approximately(_lastSyncedRotY, clientRotY) &&
            Mathf.Approximately(_lastSyncedRotX, clientRotX) &&
            Mathf.Approximately(_lastSyncedPos.x, clientPos.x) &&
            Mathf.Approximately(_lastSyncedPos.y, clientPos.y) &&
            Mathf.Approximately(_lastSyncedPos.z, clientPos.z))
        {
            return; // Данные не изменились или изменились незначительно
        }

        _lastSyncedPos = clientPos;
        _lastSyncedRotY = clientRotY;
        _lastSyncedRotX = clientRotX;

        CurrentRotationY = clientRotY; // Обновляем SyncVar (или просто серверное поле)

        // Обработка телепортации, инициированной сервером
        if (_serverTeleportTargetPosition != Vector3.zero)
        {
            CurrentPosition = _serverTeleportTargetPosition;
            _antiFakeCommands.SetPosition(_serverTeleportTargetPosition);
            transform.position = _serverTeleportTargetPosition;
            _serverTeleportTargetPosition = Vector3.zero; // Сбрасываем флаг телепортации
        }
        // Обычная обработка движения с проверкой античита
        else if (_isInputAllowed && _antiFakeCommands.CheckMovement(clientPos))
        {
            // Специальная логика для класса 2 (если это класс, который всегда находится в определенной позиции)
            if (characterClassManager.curClass == 2)
            {
                clientPos = new Vector3(0f, 2048f, 0f);
            }

            _fallDamage.CalculateGround(); // Пересчитываем состояние земли
            IsGrounded = _fallDamage.isCloseToGround; // Обновляем состояние "на земле"
            CheckGroundForAntiCheat(clientPos); // Вызываем античит проверку
            CurrentPosition = clientPos; // Обновляем серверную позицию игрока

            if (IsGrounded)
            {
                GroundedYPosition = clientPos.y; // Обновляем Y-координату приземления
            }
        }
        else
        {
            // Если античит сработал или ввод не разрешен, возвращаем игрока на последнюю известную серверную позицию
            TargetSetPosition(connectionToClient, CurrentPosition);
        }

        rotX = clientRotX; // Обновляем SyncVar rotX, Mirror позаботится о синхронизации с клиентами
    }

    // TargetRpc: Отправляет команду конкретному клиенту (владельцу этого объекта)
    [TargetRpc]
    private void TargetSetPosition(NetworkConnection target, Vector3 pos)
    {
        transform.position = pos;
        CurrentPosition = pos; // Обновляем локальную позицию клиента
    }

    // TargetRpc: Отправляет команду конкретному клиенту для установки вращения
    [TargetRpc]
    private void TargetSetRotation(NetworkConnection target, float rotY)
    {
        _localPlayerRotationY = rotY;
        CurrentRotationY = rotY; // Обновляем локальную переменную и публичную SyncVar (если бы она была)
        transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        // Попытка обновить вращение в FirstPersonController, если он есть
        try
        {
            FirstPersonController component = GetComponent<FirstPersonController>();
            if (component != null)
            {
                // Предполагается, что m_MouseLook.SetRotation принимает вращение по Y
                // Если нет, возможно, понадобится адаптировать
                component.m_MouseLook.SetRotation(rotY);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting FirstPersonController rotation: {ex.Message}");
            // Не "глотать" исключение, а логгировать его для отладки
        }
    }

    // Хук для SyncVar rotX: вызывается на всех клиентах (включая владельца) при изменении rotX
    private void OnRotXChanged(float oldRotX, float newRotX)
    {
        // Обновляем вращение камеры на всех клиентах (если объект не локальный игрок,
        // то это вращение камеры другого игрока)
        if (_playerCameraTransform != null)
        {
            _playerCameraTransform.localRotation = Quaternion.Euler(newRotX, 0f, 0f);
        }
    }

    // ====================================================================================
    // --- МЕТОДЫ УПРАВЛЕНИЯ КЛИЕНТОМ/СЕРВЕРОМ ---
    // ====================================================================================

    // Клиентский метод для установки вращения
    [Client]
    public void ClientSetRotation(float rotY)
    {
        // isClient - это более точная проверка, чем NetworkClient.active для методов,
        // которые могут быть вызваны только на клиенте.
        if (!isClient)
        {
            Debug.LogWarning("[Client] function 'ClientSetRotation' called on server");
        }
        else
        {
            _localPlayerRotationY = rotY;
            // Note: Здесь не происходит обновления NetworkRotationY или трансформа,
            // это просто установка локальной переменной.
            // Если нужно визуальное обновление на клиенте, это должно быть сделано здесь.
        }
    }

    // Серверный метод для установки позиции игрока (например, для телепортации)
    [Server]
    public void SetPosition(Vector3 newPos)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'SetPosition' called on client");
            return;
        }

        _serverTeleportTargetPosition = newPos; // Устанавливаем целевую позицию для телепортации
        CurrentPosition = newPos; // Обновляем серверную позицию
        transform.position = newPos; // Немедленно перемещаем серверный объект
        _antiFakeCommands.SetPosition(newPos); // Обновляем позицию в античите

        // Сбрасываем состояние падения при телепортации
        _fallDamage.isGrounded = true;
        _fallDamage.isCloseToGround = true;
        _fallDamage.previousHeight = newPos.y;

        // Отправляем TargetRpc, чтобы уведомить владельца объекта о новой позиции
        TargetSetPosition(connectionToClient, newPos);
    }

    // Серверный метод для установки вращения игрока
    [Server]
    public void SetRotation(float rotY)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'SetRotation' called on client");
            return;
        }

        CurrentRotationY = rotY; // Обновляем серверное вращение
        _localPlayerRotationY = rotY; // Обновляем локальное вращение на сервере

        // Отправляем TargetRpc, чтобы уведомить владельца объекта о новом вращении
        TargetSetRotation(connectionToClient, rotY);
    }

    // Серверный метод для разрешения/запрета ввода игрока
    [Server]
    public void SetAllowInput(bool allow)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'SetAllowInput' called on client");
        }
        else
        {
            _isInputAllowed = allow;
        }
    }

    // ====================================================================================
    // --- АНТИЧИТ И ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---
    // ====================================================================================

    // Проверка на нахождение в воздухе (античит)
    private void CheckGroundForAntiCheat(Vector3 currentPos)
    {
        // Если игрок является SCP-106 или другим специальным классом, или уже на земле, сбросить счетчик полета
        if (characterClassManager.curClass == 2 || characterClassManager.curClass == -1 || IsGrounded || characterClassManager.curClass == 7)
        {
            FlyTime = 0f;
            _wasUsingPortal = false;
            GroundedYPosition = currentPos.y;
            return;
        }

        FlyTime += Time.deltaTime;

        // Специальная логика для SCP-106 при использовании порталов
        if (_scp106Script.iAm106 && (_scp106Script.goingViaThePortal || _wasUsingPortal))
        {
            _wasUsingPortal = true;
            if (FlyTime < 4.5f) // Время в воздухе после портала
            {
                return;
            }
        }

        // Если не на земле, проверяем падение/полет
        if (!IsGrounded)
        {
            // Если игрок упал слишком далеко
            if (GroundedYPosition < currentPos.y - 3f)
            {
                HandleFlyingViolation("*Killed by anticheat for flying (code: 1.3).");
                return;
            }
            // Обновляем самую низкую точку Y во время падения
            if (GroundedYPosition > currentPos.y)
            {
                GroundedYPosition = currentPos.y;
            }
        }

        // Дополнительная проверка на отсутствие земли под игроком (Linecast/OverlapBox)
        Vector3 raycastOrigin = currentPos;
        raycastOrigin.y -= 50f; // Луч вниз

        if (!Physics.Linecast(currentPos, raycastOrigin, _antiFakeCommands.mask))
        {
            Vector3 overlapBoxCenter = currentPos;
            overlapBoxCenter.y += 23.8f; // Центр для OverlapBox

            // Проверка OverlapBox для обнаружения земли в определенной области
            if (Physics.OverlapBox(overlapBoxCenter, new Vector3(0.5f, 25f, 0.5f), Quaternion.identity, _antiFakeCommands.mask).Length == 0)
            {
                HandleFlyingViolation("*Killed by anticheat for flying (code: 1.2).");
                return;
            }
        }

        // Общая проверка на слишком долгое пребывание в воздухе
        if (!(FlyTime < 2.2f)) // Максимальное разрешенное время полета
        {
            HandleFlyingViolation("*Killed by anticheat for flying (code: 1.1).");
        }
    }

    // Вспомогательный метод для обработки нарушения полета
    private void HandleFlyingViolation(string message)
    {
        if (!RoundStart.RoundJustStarted)
        {
            characterClassManager.GetComponent<PlayerStats>().HurtPlayer(
                new PlayerStats.HitInfo(2000000f, message, DamageTypes.Flying, 0), gameObject);
        }
        else
        {
            _unstuckNeeded = true;
        }
    }

    // Корутина для проверки застревания игрока (запускается только на сервере)
    private IEnumerator<float> UnstuckCheckRoutine()
    {
        while (RoundStart.RoundJustStarted)
        {
            if (_unstuckNeeded)
            {
                Unstuck();
                _unstuckNeeded = false;
            }
            yield return Timing.WaitForSeconds(1f);
            CheckGroundForAntiCheat(transform.position); // Проверяем позицию на сервере
        }
    }

    // Внутренний метод для "разблокировки" игрока (серверный)
    [Server]
    internal void Unstuck()
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'Unstuck' called on client");
            return;
        }

        Vector3 respawnPoint = NonFacilityCompatibility.currentSceneSettings.constantRespawnPoint;
        if (respawnPoint == Vector3.zero)
        {
            // Если нет фиксированной точки возрождения, используем случайную
            SetPosition(UnityEngine.Object.FindObjectOfType<SpawnpointManager>().GetRandomPosition(characterClassManager.curClass).transform.position);
        }
        else
        {
            SetPosition(respawnPoint); // Используем фиксированную точку
        }
    }
}