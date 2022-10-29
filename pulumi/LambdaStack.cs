using Pulumi;
using Pulumi.Aws.Lambda;

class LambdaStack : Stack
{
    public LambdaStack()
    {
        var lambdaJoinGame = new Function("join-game-2", new FunctionArgs
        {
            Runtime = "dotnetcore3.1",
            Code = new FileArchive("../join-game-2/src/join-game-2/bin/Release/netcoreapp3.1/publish"),
            Handler = "join-game-2::join_game_2.Function::FunctionHandler",
            Role = "arn:aws:iam::137312912338:role/service-role/game-server-role",
            Timeout = 30
        });
        
        var lambdaGameMessaging = new Function("game-messaging-2", new FunctionArgs
        {
            Runtime = "dotnetcore3.1",
            Code = new FileArchive("../game-messaging-2/src/game-messaging-2/bin/Release/netcoreapp3.1/publish"),
            Handler = "game-messaging-2::game_messaging_2.Function::FunctionHandler",
            Role = "arn:aws:iam::137312912338:role/service-role/game-server-role",
            Timeout = 30
        });
        
        var lambdaDisconnectGame = new Function("disconnect-game-2", new FunctionArgs
        {
            Runtime = "dotnetcore3.1",
            Code = new FileArchive("../disconnect-game-2/src/disconnect-game-2/bin/Release/netcoreapp3.1/publish"),
            Handler = "disconnect-game-2::disconnect_game_2.Function::FunctionHandler",
            Role = "arn:aws:iam::137312912338:role/service-role/game-server-role",
            Timeout = 30
        });
    }
}
