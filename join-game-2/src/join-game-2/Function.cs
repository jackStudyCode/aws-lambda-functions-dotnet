using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Newtonsoft.Json;
using Amazon.Lambda.Core;
using Amazon.Runtime;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace join_game_2
{
    public class Function
    {
        
        const string PLAYING_OP = "11";
        
        private async void SendToClient(APIGatewayProxyRequest request, string connectionId, GameMessage gameMessage)
        {
            var domainName = request.RequestContext.DomainName;
            var stage = request.RequestContext.Stage;
            var endPoint = $"https://{domainName}/{stage}";

            Console.WriteLine("API Gateway management endpoint: " + endPoint);
            Console.WriteLine("connectionId: " + connectionId);

            var data = JsonConvert.SerializeObject(gameMessage);
            Console.WriteLine("game Message data: " + data);

            var stream = new MemoryStream(UTF8Encoding.UTF8.GetBytes(data));

            var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
            {
                ServiceURL = endPoint
            });
            
            var postConnectionRequest = new PostToConnectionRequest
            {
                ConnectionId = connectionId,
                Data = stream
            };

            try
            {
                stream.Position = 0;
                var response = await apiClient.PostToConnectionAsync(postConnectionRequest);
                Console.WriteLine("PostToConnectionAsync response code: " + response.HttpStatusCode);
            }
            catch (AmazonServiceException exception)
            {
                Console.WriteLine("send message to client failed. StatusCode: " + exception.StatusCode);
                Console.WriteLine("ErrorCode: " + exception.ErrorCode);
            }
        }
        
        public async Task<bool> AddConnectionId(APIGatewayProxyRequest request, string connectionId, GameSessionDbOperations gameSessionDbOperations)
        {
            var gameSessions = gameSessionDbOperations.GetGameSessionAsync();
            Console.WriteLine("Game session data: " + JsonConvert.SerializeObject(gameSessions.Result));

            bool putResult = false;

            if (gameSessions.Result != null && gameSessions.Result.Length < 1)
            {
                // create new game session 
                Console.WriteLine("No sessions exist, creating session...");
                Console.WriteLine("Player1 connectionId: " + connectionId);

                GameSession newGameSession = new GameSession
                {
                    uuid = Guid.NewGuid().ToString(),
                    player1 = connectionId,
                    player2 = "empty",
                    gameStatus = "active"
                };

                putResult = await gameSessionDbOperations.PutGameSessionDataAsync(newGameSession);
            }
            else if(gameSessions.Result != null)
            {
                // add player to existing session as player2
                Console.WriteLine("Session exists, adding player2 to existing session");

                var currGameSession = gameSessions.Result[0];

                GameSession updatedGameSession = new GameSession
                {
                    uuid = currGameSession.uuid,
                    player1 = currGameSession.player1,
                    player2 = connectionId,
                    gameStatus = currGameSession.gameStatus
                };
                
                putResult = await gameSessionDbOperations.UpdateGameSessionDataAsync(updatedGameSession);
                
                // inform player 1 game started. Cannot yet send message to player2.
                var player1Message = new GameMessage(updatedGameSession.uuid, PLAYING_OP);
                SendToClient(request, updatedGameSession.player1, player1Message);
            }

            return putResult;
        }

                
        /// <summary>
        /// A function that handles a join game request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string connectionId = request.RequestContext.ConnectionId;
                Console.WriteLine("Connect event received:\n" + request.ToString());

                GameSessionDbOperations gameSessionDbOperations =
                    new GameSessionDbOperations(new AmazonDynamoDBClient());

                var addResult = await AddConnectionId(request, connectionId, gameSessionDbOperations);
                int statusCode = 200;
                if (!addResult)
                {
                    statusCode = 400;
                }

                return new APIGatewayProxyResponse
                {
                    StatusCode = statusCode
                };
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in join game: " + e.Message);
                Console.WriteLine(e.StackTrace);
                
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Failed in game messaging: {e.Message}"
                };
            }
        }
    }
}
