namespace AzureDiagnostics

open System
open System.Data.Services.Client
open System.Diagnostics
open System.Text

open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.StorageClient

module private Constants =
    [<Literal>]
    let DEFAULT_DIAGNOSTICS_CONNECTION_STRING =
        "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"    

type TableStorageTraceListener(connectionString:string) =
    inherit TraceListener()
              
    let tableStorageGuard = new Object()
    let mutable tableStorage : CloudTableClient option = None

    let traceLogGuard = new Object()
    let mutable traceLog : LogEntry list = []
    
    static member DIAGNOSTICS_TABLE = "DevLogsTable"
    new() = new TableStorageTraceListener(Constants.DEFAULT_DIAGNOSTICS_CONNECTION_STRING)

    [<ThreadStatic>]
    [<DefaultValue(false)>]
    static val mutable private messageBuffer : StringBuilder option
    
    let MessageBuffer =
        if TableStorageTraceListener.messageBuffer.IsNone 
            then TableStorageTraceListener.messageBuffer <- Some(new StringBuilder())
        TableStorageTraceListener.messageBuffer.Value

    let TableStorage =
        if tableStorage.IsNone
            then lock tableStorageGuard (fun() -> 
                if tableStorage.IsNone 
                    then do
                        let tableClient = 
                            CloudStorageAccount
                                .FromConfigurationSetting(connectionString)
                                .CreateCloudTableClient()
                        if tableClient.CreateTableIfNotExist(TableStorageTraceListener.DIAGNOSTICS_TABLE)
                            then MessageBuffer.AppendLine(
                                    "TableStorageTraceListener: "
                                    + TableStorageTraceListener.DIAGNOSTICS_TABLE 
                                    + " created")
                                |> ignore
                        tableStorage <- Some(tableClient)
                )
        tableStorage.Value

    member private this.appendEntry(id, eventType, eventCache:TraceEventCache) =
        let message = MessageBuffer.ToString()
        MessageBuffer.Length <- 0
        let message =
            if message.EndsWith(Environment.NewLine)
                then message.Substring(0, message.Length - Environment.NewLine.Length)
                else message
        if message.Length > 0 then do
            let entry = 
                new LogEntry
                    ( 
                        PartitionKey = String.Format("{0:D10}", eventCache.Timestamp >>> 30),
                        RowKey = String.Format("{0:D19}", eventCache.Timestamp),
                        EventTickCount = eventCache.Timestamp,
                        Level = (int) eventType,
                        EventId = id,
                        Pid = eventCache.ProcessId,
                        Tid = eventCache.ThreadId,
                        Message = message
                    )
            lock traceLogGuard (fun () ->
                traceLog <- entry :: traceLog
                )
            
    override this.Flush() =
        let context = TableStorage.GetDataServiceContext()
        context.MergeOption <- MergeOption.AppendOnly
        lock traceLogGuard (fun() ->
            traceLog
            |> List.iter (fun entry ->
                context.AddObject(TableStorageTraceListener.DIAGNOSTICS_TABLE, entry)
            )
            traceLog <- []
        )
        if context.Entities.Count > 0 
            then (context.BeginSaveChangesWithRetries(
                      SaveChangesOptions.None
                    , (fun ar -> context.EndSaveChangesWithRetries(ar) 
                                 |> ignore)
                    , null
                )) |> ignore

    override this.IsThreadSafe =
        true

    override this.Write(message:string) =
        MessageBuffer.Append(message)
            |> ignore

    override this.WriteLine(message:string) =
        MessageBuffer.AppendLine(message)
            |> ignore

    override this.TraceData
        (
            eventCache:TraceEventCache
            , source:string
            , eventType:TraceEventType
            , id:int
            , data:Object) =
        do 
            base.TraceData(eventCache, source, eventType, id, data)
            this.appendEntry(id, eventType, eventCache)
            
    override this.TraceData
        (
            eventCache:TraceEventCache
            , source:string
            , eventType:TraceEventType
            , id:int
            , data:Object array) =
        do 
            base.TraceData(eventCache, source, eventType, id, data)
            this.appendEntry(id, eventType, eventCache)
            
    override this.TraceEvent
        (
            eventCache:TraceEventCache
            , source:string
            , eventType:TraceEventType
            , id:int) =
        do 
            base.TraceEvent(eventCache, source, eventType, id)
            this.appendEntry(id, eventType, eventCache)
            
    override this.TraceEvent
        (
            eventCache:TraceEventCache
            , source:string
            , eventType:TraceEventType
            , id:int
            , format:string
            , [<ParamArray>] args:Object array) =
        do 
            base.TraceEvent(eventCache, source, eventType, id, format, args)
            this.appendEntry(id, eventType, eventCache)
            
    override this.TraceEvent
        (
            eventCache:TraceEventCache
            , source:string
            , eventType:TraceEventType
            , id:int
            , message:string) =
        do 
            base.TraceEvent(eventCache, source, eventType, id, message)
            this.appendEntry(id, eventType, eventCache)
               
    override this.TraceTransfer
        (
            eventCache:TraceEventCache
            , source:string
            , id:int
            , message:string
            , relatedActivityId:Guid) =
        do 
            base.TraceTransfer(eventCache, source, id, message, relatedActivityId)
            this.appendEntry(id, TraceEventType.Transfer, eventCache)
               
       