# Интеграции AntigravityQuota со сторонними строками состояния macOS

Готовые легковесные плагины и скрипты для отображения квот моделей Google Antigravity в популярных строках состояния macOS: **SketchyBar**, **SwiftBar** и **BitBar / xbar**.

---

## Возможности и форматы

| Строка состояния | Скрипт / Плагин | Интервал | Особенности |
| :--- | :--- | :--- | :--- |
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

## 3. Автоматический поиск исполняемого файла CLI

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

## 4. Цветовая шкала статусов

Во всех плагинах используется единая цветовая палитра:
- 🟢 **Зеленый** (> 50%): достаточный запас квоты для комфортной работы.
- 🟠 **Оранжевый** (20% - 50%): умеренный остаток, рекомендуется следить за расходом.
- 🔴 **Красный** (< 20%): критический остаток квоты, скорое исчерпание пула.
- ⚪ **Серый**: процесс `language_server` не запущен или Antigravity оффлайн.

---

## 5. Проверка работы из терминала

Любой скрипт можно запустить напрямую из консоли для проверки вывода:

```bash
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
