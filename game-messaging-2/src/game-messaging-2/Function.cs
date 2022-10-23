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

namespace game_messaging_2
{
    public class Function
    {
        private const string REQUEST_START_OP = "1";
        private const string THROW_OP = "5";
        private const string BLOCK_HIT_OP = "9";
        private const string YOU_WON = "91";
        private const string YOU_LOST = "92";
        private const string PLAYING_OP = "11";
        
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
                await apiClient.PostToConnectionAsync(postConnectionRequest);
            }
            catch (AmazonServiceException exception)
            {
                Console.WriteLine("send message to client failed. StatusCode: " + exception.StatusCode);
                Console.WriteLine("ErrorCode: " + exception.ErrorCode);
            }
        }
        
        /// <summary>
        /// A function that handles game messaging
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string requestBodyStr = JsonConvert.SerializeObject(request.Body, Formatting.Indented);
                Console.WriteLine("Connect event body received: " + requestBodyStr);

                GameMessage gameMessage = JsonConvert.DeserializeObject<GameMessage>(request.Body);
                if (gameMessage != null)
                {
                    Console.WriteLine("request body uuid: " + gameMessage.uuid + "; request body opcode: " +
                                      gameMessage.opcode);
                }
                else
                {
                    Console.WriteLine("request body is null!!!");
                }

                var connectionIdForCurrentRequest = request.RequestContext.ConnectionId;
                Console.WriteLine("Current connection id: " + connectionIdForCurrentRequest);

                if (gameMessage != null && gameMessage.opcode != null)
                {
                    var gameSessionDBOperations = new GameSessionDBOperations(new AmazonDynamoDBClient());
                    var gameSessions = await gameSessionDBOperations.GetGameSessionAsync(connectionIdForCurrentRequest);
                    var currGameSession = gameSessions[0];
                    Console.WriteLine("getGameSession: " + currGameSession.ToString());

                    switch (gameMessage.opcode)
                    {
                        case REQUEST_START_OP:
                            Console.WriteLine("opcode 1 hit");

                            // we check for closed to handle an edge case where if player1 joins and immediately quits,
                            // we mark closed to make sure a player2 can't join an abandoned game session
                            var opcodeStart = "0";
                            if (!currGameSession.gameStatus.Equals("closed") &&
                                !currGameSession.player2.Equals("empty"))
                            {
                                opcodeStart = PLAYING_OP;
                            }

                            var startPlayMessage = new GameMessage(currGameSession.uuid, opcodeStart);
                            SendToClient(request, connectionIdForCurrentRequest, startPlayMessage);

                            break;

                        case THROW_OP:
                            Console.WriteLine("opcode 5 hit");

                            var sendToConnectionId = connectionIdForCurrentRequest;
                            if (currGameSession.player1.Equals(connectionIdForCurrentRequest))
                            {
                                // request came from player1, just send out to player2
                                sendToConnectionId = currGameSession.player2;
                            }
                            else
                            {
                                // request came from player2, just send out to player1
                                sendToConnectionId = currGameSession.player1;
                            }

                            Console.WriteLine("sending throw message to: " + sendToConnectionId);
                            string message = "other player threw ball";
                            var throwBallMessage = new GameMessage(currGameSession.uuid, THROW_OP, message);
                            SendToClient(request, sendToConnectionId, throwBallMessage);

                            break;

                        case BLOCK_HIT_OP:
                            // block hit, send game over
                            Console.WriteLine("opcode 9 hit");

                            if (currGameSession.player1.Equals(connectionIdForCurrentRequest))
                            {
                                // player1 was the winner
                                var wonMessage = new GameMessage(currGameSession.uuid, YOU_WON);
                                SendToClient(request, currGameSession.player1, wonMessage);


                                var lostMessage = new GameMessage(currGameSession.uuid, YOU_LOST);
                                SendToClient(request, currGameSession.player2, lostMessage);
                            }
                            else
                            {
                                // player2 was the winner
                                var lostMessage = new GameMessage(currGameSession.uuid, YOU_LOST);
                                SendToClient(request, currGameSession.player1, lostMessage);

                                var wonMessage = new GameMessage(currGameSession.uuid, YOU_WON);
                                SendToClient(request, currGameSession.player2, wonMessage);
                            }

                            break;
                    }
                }

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200
                };

            }
            catch (Exception e)
            {
                Console.WriteLine("Error in game messaging: " + e.Message);
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
