open System
open System.Configuration
open System.Data.Services.Client
open System.Linq
open System.Threading

open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.StorageClient

open AzureDiagnostics

[<EntryPoint>]
let main argv = 

    let connectionString = 
        if argv.Length = 0
            then "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" 
            else argv.[0]
    let tableStorage = 
        CloudStorageAccount
            .Parse(ConfigurationManager.AppSettings.[connectionString])
            .CreateCloudTableClient()
    if tableStorage.CreateTableIfNotExist(TableStorageTraceListener.DIAGNOSTICS_TABLE)
        then Console.WriteLine("Logviewer: {0} created", TableStorageTraceListener.DIAGNOSTICS_TABLE)
        else Console.WriteLine("Logviewer: {0} already exists", TableStorageTraceListener.DIAGNOSTICS_TABLE)

    let lastPartitionKey = ref String.Empty
    let lastRowKey = ref String.Empty

    let queryLogTable() =
        let query = 
            tableStorage
                .GetDataServiceContext()
                .CreateQuery(TableStorageTraceListener.DIAGNOSTICS_TABLE)
                .Where(fun (entry:LogEntry) ->
                    String.Compare(entry.PartitionKey, !lastPartitionKey, System.StringComparison.Ordinal) > 0
                    ||
                    (
                        entry.PartitionKey = !lastPartitionKey 
                        &&
                        String.Compare(entry.RowKey, !lastRowKey, System.StringComparison.Ordinal) > 0
                    ))
                :?> DataServiceQuery<LogEntry>
        query.Execute()
        |> Seq.iter (fun entry ->
                Console.WriteLine("{0} - {1}", entry.Timestamp, entry.Message)
                lastPartitionKey.Value <- entry.PartitionKey
                lastRowKey.Value <- entry.RowKey
                )
               
    let timer = 
        new Timer(
            (fun _ ->
                ProgressIndicator.Disable()
                queryLogTable()
                ProgressIndicator.Enable()
            )
            ,null
            ,0
            ,10000
       )

    Console.ReadLine() |> ignore
    0 

