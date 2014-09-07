package clients;

interface IJsonSerializationClientAdapter
{
    var hash(get, null) : String;
    function dequeueCallback(commandName : String) : Void;
    function handleResult(commandName : String, commandHash : String, parameters : String) : Void;
    var clientEvent : String -> String -> String -> Void;
}
