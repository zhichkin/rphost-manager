# 1C rphost manager

[Подробное описание идеи и решаемой задачи](https://rarus.ru/publications/20200518-ot-ekspertov-1c-rarus-optimizatsiya-perezapuska-rabochikh-protsessov-na-platforme-1c-8-3-15-i-vyshe-424479/#povyshennyi-raskhod-pamyati-i-vozmozhnye-prichiny)

[Скачать актуальную версию rphost-manager](https://github.com/zhichkin/rphost-manager/releases)

Служба Windows, которая следит за объёмом памяти, потребляемой каждым rphost'ом 1C по отдельности.

**Цель мониторинга:** автоматический перезапуск rphost'ов для предотвращения деградации их производительности.

В случае обнаружения превышения любым rphost'ом установленного для него лимита памяти, служба rphost-manager перенастривает свойства соответствующего рабочего сервера 1С, а именно **TemporaryAllowedProcessesTotalMemory** (временно допустимый объём памяти процессов) и **TemporaryAllowedProcessesTotalMemoryTimeLimit** (интервал превышения допустимого объёма памяти процессов), таким образом, чтобы менеджер кластера 1С начал перезапуск rphost'ов. После перезапуска служба rphost-manager возвращает эти настройки к исходным значениям.

Совместимо с версиями платформы 1С:Предприятие 8.3.15 и выше.

**Установка**
1. Установить [.NET Core 3.1](https://dotnet.microsoft.com/download).
2. Установить COMConnector 1C, выполнив команду от имени администратора:
```SQL
regsvr32 "C:\Program Files\1cv8\8.3.15.1778\bin\comcntr.dll"
```
4. Распаковать содержимое [установочного архива](https://github.com/zhichkin/rphost-manager/releases) в любой каталог.
5. Установить сервис Windows, выполнив команду от имени администратора:
```SQL
sc create "1C RpHost Manager" binPath="D:\RphostManager\rphost-manager.exe"
```

**Настройка**

- **LogSize** - размер лога программы в байтах. По достижению этого лимита файл лога перезаписывается.
- **CLSID** - идентификатор COM объекта "V83.COMConnector". Данная настройка используется только в том случае, если она заполнена. В противном случае используется ProgID "V83.COMConnector".
- **InspectionPeriodicity** - периодичность инспекции объёма памяти, используемого rphost'ами, в секундах.
- **ServerAddress** - адрес центрального сервера 1С.
- **UserName** - имя пользователя для подключения к кластеру 1С.
- **Password** - пароль пользователя для подключения к кластеру 1С.
- **WorkingServerResetWaitTime** - период ожидания переключения rphost'ов менеджером кластера 1С в нерабочее состояние в секундах.
- **WorkingProcessMemoryLimit** - лимит памяти для одного rphost'а в килобайтах. Используется только в том случае, если не определён список индивидуальных настроек для рабочих серверов **WorkingServerMemoryLimits**.
- **WorkingServerMemoryLimits** - список рабочих серверов 1С, которые нужно инспектировать, и лимит памяти rphost'а для каждого сервера в отдельности. Если список пустой, то инспектируются все сервера кластера, а в качестве лимита памяти rphost'а используется настройка **WorkingProcessMemoryLimit**.

**Пример 1. Файл appsettings.json (общая для всех рабочих серверов настройка)**
```JSON
{
  "LogSize": 131072,
  "CLSID": "",
  "InspectionPeriodicity": 180,
  "ServerAddress": "tcp://MSK01:1540",
  "UserName": "",
  "Password": "",
  "WorkingServerResetWaitTime": 10,
  "WorkingProcessMemoryLimit": 4194304,
  "WorkingServerMemoryLimits": {},
  "HostOptions": {
    "ShutdownTimeout": "00:00:30"
  }
}
```

**Пример 2. Файл appsettings.json (индивидуальная настройка рабочих серверов)**
```JSON
{
  "LogSize": 262144,
  "CLSID": "181E893D-73A4-4722-B61D-D604B3D67D47",
  "InspectionPeriodicity": 300,
  "ServerAddress": "tcp://MSK01:1540",
  "UserName": "",
  "Password": "",
  "WorkingServerResetWaitTime": 10,
  "WorkingProcessMemoryLimit": 2097152,
  "WorkingServerMemoryLimits": {
    "MSK01-SRV01": 1048576,
    "MSK01-SRV02": 2097152
  },
  "HostOptions": {
    "ShutdownTimeout": "00:00:30"
  }
}
```
