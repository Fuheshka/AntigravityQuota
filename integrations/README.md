# Интеграции AntigravityQuota со сторонними строками состояния и лаунчерами macOS

Готовые легковесные плагины и скрипты для отображения квот моделей Google Antigravity в популярных лаунчерах и строках состояния macOS: **Raycast**, **SketchyBar**, **SwiftBar** и **BitBar / xbar**.

---

## Возможности и форматы

| Лаунчер / Строка состояния | Скрипт / Плагин | Интервал | Особенности |
| :--- | :--- | :--- | :--- |
| **Raycast** | [`raycast/antigravity-quota.sh`](raycast/antigravity-quota.sh) | `inline` (1 мин) / `fullOutput` | Компактный вывод в строке поиска Raycast, статус квот моделей, таймеры сброса, детальный отчет |
| **SketchyBar** | [`sketchybar/antigravity_quota.sh`](sketchybar/antigravity_quota.sh) | 30-60 сек (настраиваемый) | Nerd Font иконки (`󰛩`, `󰚩`), процент пулов, таймеры сброса, ARGB цвета (`0xff4ade80`, `0xfffb923c`, `0xfff87171`) |
| **SwiftBar** | [`swiftbar/antigravity_quota.1m.sh`](swiftbar/antigravity_quota.1m.sh) | 1 минута (`.1m`) | Компактная строка меню, выпадающее меню с пулами (5h и Weekly), детальные квоты моделей, быстрые действия |
| **BitBar / xbar** | [`swiftbar/antigravity_quota.1m.sh`](swiftbar/antigravity_quota.1m.sh) | 1 минута (`.1m`) | Полная совместимость со стандартным протоколом BitBar |

---

## 1. SketchyBar

### Установка

1. Скопируйте или создайте символическую ссылку на скрипт в папку плагинов SketchyBar:
   ```bash
   mkdir -p ~/.config/sketchybar/plugins
   ln -sf "$(pwd)/integrations/sketchybar/antigravity_quota.sh" ~/.config/sketchybar/plugins/antigravity_quota.sh
   chmod +x ~/.config/sketchybar/plugins/antigravity_quota.sh
   ```

2. Добавьте элемент в ваш `~/.config/sketchybar/sketchybarrc`:
   ```bash
   # Antigravity Quota Monitor
   sketchybar --add item antigravity_quota right \
              --set antigravity_quota \
                    update_freq=30 \
                    icon.drawing=off \
                    label.font="JetBrainsMono Nerd Font:Regular:12.0" \
                    click_script="open -a AntigravityQuota" \
                    script="$PLUGIN_DIR/antigravity_quota.sh"
   ```

3. Перезагрузите SketchyBar:
   ```bash
   sketchybar --reload
   ```

### Варианты отображения

- **Компактный режим (без таймеров)**:
  ```bash
  sketchybar --set antigravity_quota script="$PLUGIN_DIR/antigravity_quota.sh --compact"
  ```
  Вывод: `󰛩 71.4% · 󰚩 100.0%`

- **Стиль с фоновой плашкой (Pill)**:
  ```bash
  sketchybar --set antigravity_quota \
                background.color=0x20ffffff \
                background.corner_radius=6 \
                background.height=24 \
                background.padding_left=4 \
                background.padding_right=4
  ```

- **Кастомные иконки через переменные окружения**:
  ```bash
  export GEMINI_ICON="✦"
  export CLAUDE_ICON="🤖"
  ```

---

## 2. SwiftBar и BitBar

[SwiftBar](https://github.com/swiftbar/SwiftBar) и [BitBar / xbar](https://github.com/matryer/bitbar) используют плагинную модель на основе периодического запуска скриптов.

### Установка в SwiftBar

1. Узнайте путь к папке плагинов SwiftBar (настройки SwiftBar -> `Plugin Directory`, по умолчанию `~/.config/swiftbar/plugins` или `~/Documents/SwiftBar Plugins`).
2. Создайте символическую ссылку на плагин:
   ```bash
   PLUGINS_DIR="$HOME/.config/swiftbar/plugins"
   mkdir -p "$PLUGINS_DIR"
   ln -sf "$(pwd)/integrations/swiftbar/antigravity_quota.1m.sh" "$PLUGINS_DIR/antigravity_quota.1m.sh"
   chmod +x "$PLUGINS_DIR/antigravity_quota.1m.sh"
   ```
3. В строке меню появится компактный индикатор: `G 71.4% · C 100.0%`.
4. Нажмите на плагин для открытия подробного выпадающего меню:
   - Лимиты пулов Gemini и Claude (скользящее 5-часовое окно и недельный лимит).
   - Таймеры восстановления и сброса лимитов.
   - Список всех доступных моделей с их квотами (шрифт Menlo, цветовая индикация).
   - Быстрый запуск Antigravity и открытие AntigravityQuota.
   - Принудительное мгновенное обновление квот (`Refresh`).

### Установка в BitBar / xbar

Создайте символическую ссылку в папку плагинов BitBar:
```bash
ln -sf "$(pwd)/integrations/swiftbar/antigravity_quota.1m.sh" "$HOME/BitBarPlugins/antigravity_quota.1m.sh"
chmod +x "$HOME/BitBarPlugins/antigravity_quota.1m.sh"
```

---

## 3. Команда скриптов Raycast (Raycast Script Command)

[Raycast Script Commands](https://github.com/raycast/script-commands) позволяют вызывать скрипты прямо из лаунчера Raycast с мгновенным отображением статуса в строке поиска или в отдельном окне.

Скрипт: [`integrations/raycast/antigravity-quota.sh`](raycast/antigravity-quota.sh)

### Установка в Raycast

1. Откройте настройки Raycast: нажмите `⌘,` или введите в поиске `Raycast Settings`.
2. Перейдите во вкладку **Extensions**.
3. Нажмите кнопку `+` в правом верхнем/нижнем углу и выберите пункт **Add Script Directory**.
4. Укажите путь к папке `integrations/raycast`:
   ```bash
   # Либо сделайте символическую ссылку в вашу персональную папку скриптов:
   mkdir -p "$HOME/RaycastScripts"
   ln -sf "$(pwd)/integrations/raycast/antigravity-quota.sh" "$HOME/RaycastScripts/antigravity-quota.sh"
   ```
5. Убедитесь, что скрипт имеет права на исполнение:
   ```bash
   chmod +x integrations/raycast/antigravity-quota.sh
   ```
6. В списке команд Raycast появится команда **Antigravity Quota** с иконкой ⚡ в пакете *Developer Utilities*.

### Режимы работы

- **Компактный режим в строке поиска (`@raycast.mode inline`, по умолчанию)**:
  Выводит текущие остатки квот моделей и таймеры прямо в строке результатов поиска Raycast:
  `G 51.7% (4ч 23м) · C 100.0%`
  Скрипт автоматически обновляет данные в фоновом режиме раз в минуту (`@raycast.refreshTime 1m`).
  Если Antigravity или `language_server` не запущены, скрипт выводит информативное сообщение:
  `Antigravity is not running`

- **Детальный отчет в отдельном окне (`@raycast.mode fullOutput`)**:
  Чтобы просматривать подробный отчет по всем пулам (5-часовое скользящее окно и недельный лимит), остаткам по каждой индивидуальной модели (Claude Opus/Sonnet, Gemini Pro/Flash, GPT-OSS) и таймерам сброса:
  Откройте файл `integrations/raycast/antigravity-quota.sh` и замените строку:
  ```bash
  # @raycast.mode inline
  ```
  на:
  ```bash
  # @raycast.mode fullOutput
  ```
  После перезагрузки расширений в Raycast при вызове команды будет открываться полноценное окно терминала со сводкой квот.

---

## 4. Автоматический поиск исполняемого файла CLI

Все интеграционные скрипты снабжены отказоустойчивым механизмом автопоиска бинарного файла:
1. `antigravity-quota` в системной переменной `PATH`.
2. `/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota`.
3. `~/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota`.
4. Относительный путь к бандлу в репозитории проекта.
5. Локальная сборка `.build/release/AntigravityQuota`.
6. Симлинк `~/.local/bin/antigravity-quota`.

При необходимости переопределить путь используйте переменную окружения `ANTIGRAVITY_QUOTA_BIN`:
```bash
export ANTIGRAVITY_QUOTA_BIN="/path/to/AntigravityQuota"
```

---

## 5. Цветовая шкала статусов

Во всех плагинах используется единая цветовая палитра:
- 🟢 **Зеленый** (> 50%): достаточный запас квоты для комфортной работы.
- 🟠 **Оранжевый** (20% - 50%): умеренный остаток, рекомендуется следить за расходом.
- 🔴 **Красный** (< 20%): критический остаток квоты, скорое исчерпание пула.
- ⚪ **Серый**: процесс `language_server` не запущен или Antigravity оффлайн.

---

## 6. Проверка работы из терминала

Любой скрипт можно запустить напрямую из консоли для проверки вывода:

```bash
# Проверка Raycast скрипта (компактный вывод inline)
./integrations/raycast/antigravity-quota.sh

# Проверка Raycast скрипта в детальном режиме (fullOutput)
./integrations/raycast/antigravity-quota.sh --full

# Проверка SketchyBar скрипта (выведет строку с иконками)
./integrations/sketchybar/antigravity_quota.sh

# Проверка SketchyBar в компактном режиме
./integrations/sketchybar/antigravity_quota.sh --compact

# Проверка SwiftBar плагина (выведет структуру строки меню и выпадающего списка)
./integrations/swiftbar/antigravity_quota.1m.sh
```

---

## Автор и поддержка / Author & Support

Сделано с душой и любовью ❤️

**Даниил К. (Fuheshka)**

- 💬 **Telegram:** [@fuheshka](https://t.me/fuheshka)
- ✉️ **Email:** [me@kuviko.ru](mailto:me@kuviko.ru)
- ☕ **Поддержать чашкой кофе (СБП / T-Pay):** [pay.cloudtips.ru/p/7adeaa28](https://pay.cloudtips.ru/p/7adeaa28)
- 💎 **TON:** `UQC-DsraaDQRjUjG9oPRkt5nGlMgxKY-pjMC6xeeYGfxiu9a`
