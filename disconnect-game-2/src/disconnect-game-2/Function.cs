using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.Lambda.Core;
using Amazon.Runtime;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace disconnect_game_2
{
    public class Function
    {
        private async void DisconnectClient(APIGatewayProxyRequest request, string connectionId)
        {
            var domainName = request.RequestContext.DomainName;
            var stage = request.RequestContext.Stage;
            var endPoint = $"https://{domainName}/{stage}";

            Console.WriteLine("API Gateway management endpoint: " + endPoint);
            Console.WriteLine("connectionId: " + connectionId);
            
            var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
            {
                ServiceURL = endPoint
            });

            var deleteConnectionRequest = new DeleteConnectionRequest
            {
                ConnectionId = connectionId
            };

            try
            {
                await apiClient.DeleteConnectionAsync(deleteConnectionRequest);
            }
            catch (AmazonServiceException exception)
            {
                Console.WriteLine("delete connection failed. StatusCode: " + exception.StatusCode);
                Console.WriteLine("ErrorCode: " + exception.ErrorCode);
            }
        }
        
        private async Task<bool> CloseGame(GameSession currGameSession, GameSessionDbOperations gameSessionDbOperations)
        {
            GameSession updatedGameSession = new GameSession
            {
                uuid = currGameSession.uuid,
                player1 = currGameSession.player1,
                player2 = currGameSession.player2,
                gameStatus = "closed"
            };
            
            var closeGameResult = await gameSessionDbOperations.UpdateGameSessionDataAsync(updatedGameSession);
            return closeGameResult;
        }
        
        /// <summary>
        /// A function that handles disconnect game
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

                var gameSessions = await gameSessionDbOperations.GetGameSessionAsync(connectionId);
                int statusCode = 200;
                
                if (gameSessions != null && gameSessions.Length >= 1)
                {
                    var currGameSession = gameSessions[0];
                    bool closeResult = await CloseGame(currGameSession, gameSessionDbOperations);
                    if (!closeResult)
                    {
                        Console.WriteLine("CloseGame failed! uuid: " + currGameSession.uuid);
                    }
                    
                    if (currGameSession.player1 == connectionId)
                    {
                        // player1 disconnected, now disconnect player 2
                        if (!currGameSession.player2.Equals("empty"))
                        {
                            Console.WriteLine("Disconnecting player 2: " + currGameSession.player2);
                            DisconnectClient(request, currGameSession.player2);
                        }
                        else
                        {
                            Console.WriteLine("Player2 was never filled");
                        }
                    }
                    else
                    {
                        // player2 disconnected, now disconnect player 1
                        Console.WriteLine("Disconnecting player 1: " + currGameSession.player1);
                        DisconnectClient(request, currGameSession.player1);
                    }
                }
                else
                {
                    Console.WriteLine("Cannot find gameSession! connectionId: " + connectionId);
                    statusCode = 400;
                }

                return new APIGatewayProxyResponse
                {
                    StatusCode = statusCode
                };

            }
            catch (Exception e)
            {
                Console.WriteLine("Error in disconnect game: " + e.Message);
                Console.WriteLine(e.StackTrace);
                
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Failed in disconnect game: {e.Message}"
                };
            }
        }
    }
}
