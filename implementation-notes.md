# Implementation notes: AntigravityQuota

## 1. Выявленный механизм работы лимитов в Antigravity 2.x

- При запуске `Antigravity.app` поднимается локальный фоновый процесс `/Applications/Antigravity.app/Contents/Resources/bin/language_server` с аргументом `--csrf_token <uuid>`.
- Сервер открывает два локальных TCP-порта на `127.0.0.1` (один HTTPS с самоподписанным сертификатом и один чистый HTTP, обычно с номером на 1 больше).
- Встроенный UI Antigravity кэширует состояние квот при старте и не обновляет выпадающее меню моделей (`View Usage`), пока пользователь вручную не нажмет кнопку обновления в `Settings -> Models`.
- При нажатии кнопки обновления вызывается Connect-RPC метод:
  `POST /exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary` с телом `{"forceRefresh": true}` и заголовками `Connect-Protocol-Version: 1` и `x-codeium-csrf-token: <token>`.
- Этот метод возвращает живую сводку по обоим пулам (`Gemini Models` и `Claude and GPT models`) в двух разрезах:
  - **5-часовой скользящий лимит (`window: "5h"`)**: точная доля остатка `remainingFraction` и UTC-время полного сброса `resetTime`.
  - **Недельный лимит (`window: "weekly"`)**: доля остатка и точное время сброса.
- Дополнительно метод `GetCascadeModelConfigData` возвращает список всех конкретных моделей (`Gemini 3.8 Flash (High)`, `Claude Opus 4.6 (Thinking)` и др.) с привязкой к их квоте.

## 2. Архитектурные решения и компромиссы (Ponytail Mode)

- **Отказ от модификации `app.asar`**: В `Antigravity.app` включена проверка SHA-256 целостности Electron ASAR и подписи Gatekeeper. Любой патч внутри бандла ломал бы подпись и слетал при каждом автообновлении. Внешний нативный демон (`LSUIElement = true`) работает полностью независимо и не требует вмешательства в файлы `Antigravity.app`.
- **Отсутствие прав Accessibility (TCC)**: Для отслеживания активности окна Antigravity вместо `CGEventTap` или `AXUIElement` используется системный центр уведомлений `NSWorkspace.didActivateApplicationNotification` и `NSWorkspace.shared.frontmostApplication`. Это избавляет пользователя от выдачи спецправ в «Конфиденциальность и безопасность».
- **Два режима плавающего HUD-виджета**:
  - *Развернутая карточка*: показывает прогресс-бары 5-часового лимита, проценты, таймер обратного отсчета и недельный остаток по обоим пулам.
  - *Компактная таблетка (Pill Mode)*: свернутый вид `● G 85.4% 1ч 20м · ● C 100%` для минимального занятия места поверх интерфейса Antigravity.
- **Устойчивость к перезапуску**: Если `Antigravity.app` был перезапущен и `language_server` сменил PID, порт или CSRF-токен, `QuotaClient` при первой ошибке соединения автоматически пересканирует `ps` и `lsof` и прозрачно переподключается.

## 3. Генерация и упаковка фирменной иконки AppIcon.icns (Промпт 1.1)

- **Точная обрезка и маскирование squircle-краев (`Resources/AppIcon.png`)**: Из исходного рендера `1024x1024` выделяется чистый корпус squircle (`[155..869]`), масштабируется до стандарта Apple HIG (`832x832` в центре холста `1024x1024` с полями по `96px`) и маскируется сглаженной `4x`-суперсэмплированной маской со скруглением `R=200px` и мягкой тенью (`GaussianBlur(radius=18)`, смещение `+12px` по Y). Это полностью убирает внешний фон по углам.
- **Нативная сборка `.icns` (`scripts/generate_icns.sh`)**: Скрипт генерирует все 10 стандартных разрешений (`16x16`..`512x512` `@1x/@2x`, вплоть до `1024x1024`) через системную утилиту `sips` и упаковывает их в `Resources/AppIcon.icns` через `iconutil -c icns`.
- **Интеграция в сборку (`scripts/build_app.sh`)**: При сборке `AntigravityQuota.app` в `Contents/Resources/AppIcon.icns` копируется фирменная иконка проекта (с автогенерацией через `scripts/generate_icns.sh`, если файл отсутствует).

## 4. Пайплайн упаковки релиза, брендированный DMG и ZIP (Промпт 1.2)

- **Дизайн фона по стандарту `macos-dmg-designer` (`scripts/build_dmg_background.py`)**:
  - Создан холст `660x440` (1x) и `1320x880` (2x) в темном стиле (`#1a1a1e` -> `#242429`) с внешней рамкой `rounded_rectangle([16, 16, 644, 404])` с нижним отступом `36px`, предотвращающим обрезку из-за высоты заголовка Finder (~30px).
  - Нанесены контрастные белые карточки подписей (`y = 208..242`, высота `34px`, заливка `rgba(255, 255, 255, 0.94)`, радиус `8px`) под иконками `128px` на `y = 140` (`left_x = 160`, `right_x = 500`). Карточки находятся строго под нижней границей иконки (`y = 204`) с нулевым наложением и 100% читаемостью текста.
  - Нарисована направляющая стрелка `AntigravityQuota.app -> Applications` (`x = 245..415`, `y = 140`) и двуязычные инструкции по установке (EN/RU).
  - Сгенерирован мульти-резолюшн Retina TIFF `Resources/dmg_background.tiff` через `/usr/bin/tiffutil -cathidpicheck`.
- **Сборка и финализация геометрии окна Finder (`scripts/finalize_dmg_layout.py`)**:
  - Временный образ монтируется в RW-режиме, все системные файлы (`.background`, `.VolumeIcon.icns`, `.DS_Store`, `.Trashes`, `.fseventsd`) скрываются флагами `chflags hidden` и `SetFile -a V`.
  - Через AppleScript скрытые элементы позиционируются на `(330, 500)` — строго под нижней границей окна (`y = 440`), что предотвращает появление горизонтального скроллбара.
  - Окно Finder жестко фиксируется в координатах `{200, 120, 860, 560}` (`660x440`), отключаются тулбар и статусбар, задается размер иконок `128px`.
  - Образ сжимается в `UDZO` с максимальным сжатием (`zlib-level=9`).
- **Сквозной скрипт релиза (`scripts/package_release.sh`)**:
  - Компилирует бинарник (`swift build -c release`), формирует бандл `dist/AntigravityQuota.app` с Info.plist и AppIcon.
  - Подписывает ad-hoc подписью (`codesign --force --deep`).
  - Упаковывает `dist/AntigravityQuota-v1.0.0-macOS.zip` через `ditto -c -k --keepParent`.
  - Собирает и стилизует `dist/AntigravityQuota-v1.0.0-macOS.dmg` через `create-dmg` и `finalize_dmg_layout.py`.


