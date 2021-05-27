# 1C rphost manager

[Подробности идеи в статье от экспертов 1С:Рарус.](https://rarus.ru/publications/20200518-ot-ekspertov-1c-rarus-optimizatsiya-perezapuska-rabochikh-protsessov-na-platforme-1c-8-3-15-i-vyshe-424479/#povyshennyi-raskhod-pamyati-i-vozmozhnye-prichiny)

Служба Windows, которая следит за объёмом памяти, потребляемой rphost'ами 1C. В случае если один или более rphost'ов начинают потреблять больше установленного в настройках лимита памяти для одного rphost'а, служба rphost-manager перенастривает свойства соответствующего рабочего сервера 1С, а именно "TemporaryAllowedProcessesTotalMemory" и "TemporaryAllowedProcessesTotalMemoryTimeLimit" таким образом, чтобы соответствующие rphost'ы были перезапущены менеджером кластера 1С. Затем rphost-manager ждёт установленное в настройках время и возвращает настройки сервера 1С к первоначальным значениям. Запуск проверки расхода памяти rphost'ами запускается с интервалом, определяемым в настройках.

Кроме этого в настройках можно указать какие рабочие сервера кластера 1С проверять. Если в настройке **WorkingServers** файла **appsettings.json** не указано ни одного сервера, то будут проверяться rphost'ы для всех рабочих серверов кластера 1С.

Совместимо с версиями платформы 1С:Предприятие 8.3.15 и выше.

**Установка**
1. Установить [.NET Core 3.1](https://dotnet.microsoft.com/download).
2. Установить COMConnector 1C, выполнив команду от имени администратора:
```SQL
regsvr32 "C:\Program Files\1cv8\8.3.15.1778\bin\comcntr.dll"
```
4. Распаковать содержимое установочного архива в любой каталог.
5. Установить сервис WIndows, выполнив команду от имени администратора:
```SQL
sc create "1C RpHost Manager" binPath="D:\RphostManager\rphost-manager.exe"
```

**Настройка**

- **LogSize** - размер лога программы в байтах. По достижению этого лимита файл лога перезаписывается.
- **InspectionPeriodicity** - периодичность инспекции объёма памяти, используемого rphost'ами, в секундах.
- **ServerAddress** - адрес центрального сервера 1С.
- **UserName** - имя пользователя для подключения к кластеру 1С.
- **Password** - пароль пользователя для подключения к кластеру 1С.
- **WorkingServers** - список рабочих серверов 1С, которые нужно инспектировать. Если не указано, то все. Строковые значения, перечисленные через запятую.
- **WorkingServerResetWaitTime** - период ожидания переключения rphost'ов менеджером кластера 1С в нерабочее состояние в секундах.
- **WorkingProcessMemoryLimit** - лимит памяти для одного rphost'а в килобайтах.

**Пример файла настроек appsettings.json**
```JSON
{
  "LogSize": 262144,
  "InspectionPeriodicity": 180,
  "ServerAddress": "tcp://MSK01:1540",
  "UserName": "",
  "Password": "",
  "WorkingServers": [ "MSK01-SRV01.LOCAL" ],
  "WorkingServerResetWaitTime": 10,
  "WorkingProcessMemoryLimit": 4194304,
  "HostOptions": {
    "ShutdownTimeout": "00:00:30"
  }
}
```
