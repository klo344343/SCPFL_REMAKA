# SCPFL_REMAKA

Decompiled and partially recovered version of **SCP: Secret Laboratory** 8.1.3, ported to **Unity 2022.3.62f1** using **Universal Render Pipeline (URP)**.

The goal of this project is to restore and adapt the original Mono-based build of SCP:SL to modern Unity and rendering systems, aiming for a fully functional and potentially extendable version.

⚠️ **Warning:** The current state is **highly unstable** and purely for **experimental and educational purposes**. It may contain broken scripts, numerous compiler errors, and non-functional gameplay. Expect severe bugs, incomplete systems, and potential crashes. This is a work in progress, far from a playable state.

---

## 🔧 Status

This section outlines the current progress and remaining challenges:

-   ✅ Decompiled scripts partially restored
-   ✅ Project successfully opened in Unity 2022.3.62f1
-   ⚠️ URP rendering partially adapted (further shader work and material conversion needed)
-   ❌ Gameplay currently non-functional (core logic needs extensive debugging and re-implementation)
-   ❌ Many original systems (e.g., networking, physics interactions, UI logic) need to be fixed or entirely rebuilt
-   🚧 Significant refactoring required across the codebase for clarity and maintainability

---

## 🚀 Getting Started (For Contributors)

To get this project running in your Unity Editor:

1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/klo344343/SCPFL_REMAKA.git](https://github.com/klo344343/SCPFL_REMAKA.git)
    ```
2.  **Open in Unity Hub:**
    * Open Unity Hub.
    * Click "Add project from disk" and navigate to the cloned `SCPFL_REMAKA` folder.
    * Select "Add project".
3.  **Install Unity Version:**
    * Ensure you have **Unity 2022.3.62f1** installed. If not, Unity Hub will prompt you to install it.
4.  **Open the Project:** Click on the project in Unity Hub to open it in the Editor.
5.  **Troubleshooting:**
    * Expect initial compiler errors due to partial recovery. These are known issues and part of the refactoring process.
    * The project might require some package resolution or URP pipeline asset assignment if not automatically set up.

---

## 🤝 Contributions Welcome

This project is in an early **recovery**, **refactoring**, and **rebuilding** stage. Your help is invaluable!

If you're experienced with Unity, C#, or SCP:SL internals and want to contribute, here are areas where assistance is particularly needed:

* **Code Refactoring:** Cleaning up decompiled code for readability, performance, and adherence to modern C# practices.
* **Shader Adaptation:** Converting original shaders (or creating new ones) to work correctly within the Universal Render Pipeline (URP).
* **Networking Rebuilding:** Re-implementing or adapting the game's networking layer for stability and functionality.
* **Gameplay Logic Fixes:** Debugging and restoring core gameplay mechanics, object interactions, and character controls.
* **Asset Pipeline Integration:** Ensuring that all original assets (models, textures, sounds) are correctly imported and utilized within the new Unity version.
* **Bug Fixing:** Identifying and resolving compiler errors and runtime issues.
* **Documentation:** Improving project documentation in **both English and Russian** for better accessibility.

Feel free to:
-   **Submit a Pull Request** with your proposed changes.
-   **Open Issues** if you encounter broken parts, missing systems, or have feature suggestions.
-   **Discuss** potential solutions or challenges in the Issues section.

---

## 🗺️ Roadmap & Future Plans

Our long-term vision for `SCPFL_REMAKA` includes:

1.  **Full Restoration:** Achieving a state where the core gameplay mechanics from SCP:SL 8.1.3 are functional within Unity 2022.3.62f1 (URP).
2.  **Code Modernization:** Transitioning the codebase to leverage modern Unity features and C# best practices.
3.  **Community-Driven Development:** Fostering a collaborative environment for ongoing improvements and potential new features.
4.  **Educational Resource:** Serving as a learning resource for reverse engineering game projects and Unity porting, with documentation available in **both English and Russian**.

---

## 📞 Contact

For general inquiries or discussions, please use the GitHub Issues section.

---

## 🧪 Disclaimer

This is a **non-commercial, educational, and experimental** project, driven purely by passion and curiosity.
All original assets and code belong to **Northwood Studios**. This project aims to study and understand game development techniques.
Use at your risk. This repository is not affiliated with or endorsed by Northwood Studios.

---
---

# SCPFL_REMAKA (Русская версия)

Декомпилированная и частично восстановленная версия **SCP: Secret Laboratory** 8.1.3, портированная на **Unity 2022.3.62f1** с использованием **Universal Render Pipeline (URP)**.

Цель этого проекта — восстановить и адаптировать оригинальную сборку SCP:SL на основе Mono к современным системам Unity и рендеринга, стремясь к полностью функциональной и потенциально расширяемой версии.

⚠️ **Внимание:** Текущее состояние **крайне нестабильно** и предназначено исключительно для **экспериментальных и образовательных целей**. Оно может содержать сломанные скрипты, многочисленные ошибки компилятора и неработающий геймплей. Ожидайте серьезные ошибки, незавершенные системы и возможные сбои. Это незавершенный проект, далекий от играбельного состояния.

---

## 🔧 Статус

В этом разделе описан текущий прогресс и оставшиеся задачи:

-   ✅ Декомпилированные скрипты частично восстановлены
-   ✅ Проект успешно открыт в Unity 2022.3.62f1
-   ⚠️ Рендеринг URP частично адаптирован (требуется дополнительная работа с шейдерами и конвертация материалов)
-   ❌ Геймплей в настоящее время не функционирует (основная логика требует обширной отладки и повторной реализации)
-   ❌ Многие оригинальные системы (например, сетевая часть, физические взаимодействия, логика пользовательского интерфейса) нуждаются в исправлении или полной перестройке
-   🚧 Требуется значительный рефакторинг всей кодовой базы для улучшения читаемости и удобства поддержки

---

## 🚀 Начало работы (Для участников)

Чтобы запустить этот проект в вашем Unity Editor:

1.  **Клонируйте репозиторий:**
    ```bash
    git clone [https://github.com/klo344343/SCPFL_REMAKA.git](https://github.com/klo344343/SCPFL_REMAKA.git)
    ```
2.  **Откройте в Unity Hub:**
    * Откройте Unity Hub.
    * Нажмите "Добавить проект с диска" (Add project from disk) и перейдите в склонированную папку `SCPFL_REMAKA`.
    * Выберите "Добавить проект" (Add project).
3.  **Установите версию Unity:**
    * Убедитесь, что у вас установлена **Unity 2022.3.62f1**. Если нет, Unity Hub предложит вам ее установить.
4.  **Откройте проект:** Нажмите на проект в Unity Hub, чтобы открыть его в редакторе.
5.  **Устранение неполадок:**
    * Ожидайте начальных ошибок компилятора из-за частичного восстановления. Это известные проблемы и часть процесса рефакторинга.
    * Проект может потребовать разрешения некоторых пакетов или назначения ассета пайплайна URP, если он не настроен автоматически.

---

## 🤝 Приглашаем к участию

Этот проект находится на ранней стадии **восстановления**, **рефакторинга** и **перестройки**. Ваша помощь бесценна!

Если у вас есть опыт работы с Unity, C# или внутренними механизмами SCP:SL и вы хотите помочь, вот области, где особенно нужна помощь:

* **Рефакторинг кода:** Очистка декомпилированного кода для улучшения читаемости, производительности и соответствия современным практикам C#.
* **Адаптация шейдеров:** Конвертация оригинальных шейдеров (или создание новых) для правильной работы в Universal Render Pipeline (URP).
* **Перестройка сетевой части:** Повторная реализация или адаптация сетевого уровня игры для стабильности и функциональности.
* **Исправление логики геймплея:** Отладка и восстановление основной механики геймплея, взаимодействия объектов и управления персонажами.
* **Интеграция конвейера ассетов:** Обеспечение правильного импорта и использования всех оригинальных ассетов (моделей, текстур, звуков) в новой версии Unity.
* **Исправление ошибок:** Выявление и устранение ошибок компилятора и проблем во время выполнения.
* **Документация:** Улучшение документации проекта **как на английском, так и на русском языках** для лучшей доступности.

Вы можете:
-   **Отправить Pull Request** с предложенными вами изменениями.
-   **Открывать Issues**, если вы обнаружите неработающие части, отсутствующие системы или у вас есть предложения по функциям.
-   **Обсуждать** потенциальные решения или проблемы в разделе Issues.

---

## 🗺️ Дорожная карта и планы на будущее

Наше долгосрочное видение для `SCPFL_REMAKA` включает:

1.  **Полное восстановление:** Достижение состояния, при котором основные игровые механики SCP:SL 8.1.3 будут функционировать в Unity 2022.3.62f1 (URP).
2.  **Модернизация кода:** Переход кодовой базы на использование современных функций Unity и лучших практик C#.
3.  **Развитие, управляемое сообществом:** Создание среды для совместной работы над постоянными улучшениями и потенциальными новыми функциями.
4.  **Образовательный ресурс:** Использование проекта в качестве учебного пособия по реверс-инжинирингу игровых проектов и портированию на Unity, с документацией, доступной **как на английском, так и на русском языках**.

---

## 📞 Контакты

По общим вопросам или для обсуждения, пожалуйста, используйте раздел GitHub Issues.

---

## 🧪 Отказ от ответственности

Это **некоммерческий, образовательный и экспериментальный** проект, движимый исключительно энтузиазмом и любопытством.
Все оригинальные ассеты и код принадлежат **Northwood Studios**. Этот проект направлен на изучение и понимание методов разработки игр.
Используйте на свой страх и риск. Этот репозиторий не связан с Northwood Studios и не одобрен ими.
