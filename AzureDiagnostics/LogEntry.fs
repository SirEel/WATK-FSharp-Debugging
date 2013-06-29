namespace AzureDiagnostics

open Microsoft.WindowsAzure.StorageClient

type LogEntry() =
    inherit TableServiceEntity()

    member val EventTickCount = 0L with get, set
    member val Level = 0 with get, set
    member val EventId = 0 with get, set
    member val Pid = 0 with get, set
    member val Tid = "" with get, set
    member val Message = "" with get, set
