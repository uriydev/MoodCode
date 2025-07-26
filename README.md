# MoodCode

## О проекте

MoodCode - это инструмент для автоматического улучшения сообщений коммитов Git с использованием ИИ (Groq API). Программа анализирует ваши сообщения коммитов и, если они не соответствуют хорошим практикам, предлагает улучшенные варианты на основе внесенных изменений.

## Возможности

- Анализ качества сообщений коммитов
- Автоматическое улучшение "плохих" сообщений коммитов с помощью ИИ
- Интеграция с Git-хуками для автоматизации процесса
- Отображение списка измененных файлов
- Возможность принять, отклонить или отредактировать предложенное сообщение
- Генерация сообщений коммитов на основе изменений в коде (diff-based режим)

## Требования

- .NET 6.0 или выше
- Git установленный в системе
- API-ключ Groq (для работы с ИИ)

## Установка

1. Клонируйте репозиторий:
   ```
   git clone https://github.com/ваш-аккаунт/MoodCode.git
   cd MoodCode
   ```

2. Соберите проект:
   ```
   dotnet build
   ```

3. Установите API-ключ Groq как переменную окружения:
   ```
   # Windows (PowerShell)
   $env:GROQ_API_KEY="ваш-ключ-api"

   # Linux/macOS
   export GROQ_API_KEY="ваш-ключ-api"
   ```

## Использование

### Как отдельная утилита

```
dotnet run --project MoodCode.CLI/MoodCode.CLI.csproj "ваше сообщение коммита"
```

### Режим генерации на основе изменений в коде (diff-based)

```
dotnet run --project MoodCode.CLI/MoodCode.CLI.csproj --diff-based
```

В этом режиме MoodCode автоматически сгенерирует сообщение коммита на основе анализа изменений в коде, без необходимости указывать исходное сообщение.

### Интеграция с Git-хуками

1. Создайте файл `prepare-commit-msg` в папке `.git/hooks/` вашего репозитория:

```bash
#!/bin/sh
COMMIT_MSG_FILE=$1
COMMIT_SOURCE=$2

# Получаем текущее сообщение коммита
CURRENT_MESSAGE=$(cat $COMMIT_MSG_FILE)

# Вызываем MoodCode для анализа и улучшения сообщения
RESULT=$(dotnet run --project /путь/к/MoodCode.CLI/MoodCode.CLI.csproj "$CURRENT_MESSAGE")

# Извлекаем одобренное сообщение из результата
APPROVED_MESSAGE=$(echo "$RESULT" | grep "APPROVED_MESSAGE:" | sed 's/APPROVED_MESSAGE://')

if [ ! -z "$APPROVED_MESSAGE" ]; then
    # Заменяем сообщение коммита на улучшенное
    echo "$APPROVED_MESSAGE" > $COMMIT_MSG_FILE
fi
```

2. Сделайте скрипт исполняемым:
```
chmod +x .git/hooks/prepare-commit-msg
```

## Примеры использования

### Пример 1: Анализ простого сообщения коммита

```
$ dotnet run --project MoodCode.CLI/MoodCode.CLI.csproj "fix"

=== MoodCode Analysis ===
❌ Оригинал: "fix"
✅ Предложение: "Fix user authentication validation in login form"

📁 Измененные файлы: Auth/LoginController.cs, Models/User.cs

Принять это предложение? [Д/н/р(едактировать)]:
```

### Пример 2: Хорошее сообщение коммита

```
$ dotnet run --project MoodCode.CLI/MoodCode.CLI.csproj "Add password strength validation to registration form"

=== MoodCode Analysis ===
✅ Хорошее сообщение коммита: "Add password strength validation to registration form"
Нет необходимости в изменениях!
```

### Пример 3: Генерация сообщения на основе изменений в коде

```
$ dotnet run --project MoodCode.CLI/MoodCode.CLI.csproj --diff-based

=== MoodCode Analysis ===
✨ Сгенерировано на основе изменений: "feat: add user authentication functionality"

📁 Измененные файлы: Auth/AuthService.cs, Models/User.cs, Controllers/LoginController.cs

Принять это предложение? [Д/н/р(едактировать)]:
```

## Как это работает

1. MoodCode анализирует ваше сообщение коммита по нескольким правилам
2. Если сообщение признано "плохим", программа:
   - Получает список измененных файлов и diff изменений
   - Отправляет эту информацию в Groq API
   - Генерирует улучшенное сообщение коммита
3. В режиме diff-based:
   - MoodCode анализирует только изменения в коде (diff)
   - Генерирует сообщение коммита на основе этих изменений
   - Не требует исходного сообщения коммита
4. Пользователь может принять, отклонить или отредактировать предложенное сообщение

## Правила определения "плохих" сообщений коммитов

MoodCode считает сообщение коммита плохим, если:
- Оно слишком короткое (менее 3 символов)
- Точно совпадает с распространенными ленивыми шаблонами ("fix", "update", "wip" и т.д.)
- Начинается с префикса conventional commit, но не содержит описания
- Короткое сообщение (менее 15 символов) с общими словами
- Содержит повторяющиеся слова