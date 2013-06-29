module ProgressIndicator

open System
open System.Threading

let progressIndicator = [| '-'; '\\'; '|'; '/'; '-' |]
let mutable progressState = 0
let timer = 
    new Timer(
        (fun _ ->
            Console.Write("\r ")
            Console.Write(progressIndicator.[progressState])
            progressState <- (progressState + 1) % progressIndicator.Length
            )
        ,null
        ,Timeout.Infinite
        ,2000
    )

do Console.CursorVisible <- false

let Enable() =
    timer.Change(0, 1000)
        |> ignore

let Disable() =
    timer.Change(Timeout.Infinite, 0)
        |> ignore
    Console.Write("\r  \r")
