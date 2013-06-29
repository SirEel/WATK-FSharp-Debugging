namespace FsWeb.Controllers

open System.Diagnostics
open System.Web.Http
open FsWeb.Models

type ContactsController() =
    inherit ApiController()

    let contacts = seq { yield Contact(FirstName = "John", LastName = "Doe", Phone = "123-123-1233")
                         yield Contact(FirstName = "Jane", LastName = "Doe", Phone = "123-111-9876") }

    member x.Get() = 
        Trace.TraceInformation("ContactsController.Get() called...")
        contacts
    
    member x.Post ([<FromBody>] contact:Contact) = 
        Trace.TraceInformation("ContactsController.Post() called...")
        if contact.FirstName.Length < 3 then
            raise (System.ArgumentException( "contact", "First name must have at least 3 letters"))
        contacts |> Seq.append [ contact ] 