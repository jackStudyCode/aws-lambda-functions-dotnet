using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;

namespace join_game_2
{
    public class GameSessionDbOperations
    {
        private readonly IAmazonDynamoDB dynamoDB;

        public GameSessionDbOperations(IAmazonDynamoDB dynamoDB)
        {
            this.dynamoDB = dynamoDB;
        }

        public async Task<GameSession[]> GetGameSessionAsync()
        {
            ScanFilter scanFilter = new ScanFilter();
            scanFilter.AddCondition("player2", ScanOperator.Equal, "empty");

            var result = await dynamoDB.ScanAsync(new ScanRequest
            {
                TableName = "game-session-1",

                FilterExpression = "#p2 = :empty and #status <> :closed",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    {"#p2", "player2"},
                    {"#status", "gameStatus"}
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":empty", new AttributeValue{ S = "empty"}},
                    {":closed", new AttributeValue{ S = "closed"}}
                }

            });

            if (result != null && result.Items != null)
            {
                var gameSessions = new List<GameSession>();
                foreach (var item in result.Items)
                {
                    item.TryGetValue("uuid", out var uuid);
                    item.TryGetValue("gameStatus", out var gameStatus);
                    item.TryGetValue("player1", out var player1);
                    item.TryGetValue("player2", out var player2);
                    gameSessions.Add(new GameSession
                    {
                        uuid = uuid?.S,
                        gameStatus = gameStatus?.S,
                        player1 = player1?.S,
                        player2 = player2?.S,
                    });
                }

                return gameSessions.ToArray();
            }
            
            return Array.Empty<GameSession>();
        }
        
        public async Task<bool> PutGameSessionDataAsync(GameSession gameSession)
        {
            var request = new PutItemRequest
            {
                TableName = "game-session-1",
                Item = new Dictionary<string, AttributeValue>
                {
                    { "uuid", new AttributeValue(gameSession.uuid) },
                    { "gameStatus", new AttributeValue(gameSession.gameStatus) },
                    { "player1", new AttributeValue(gameSession.player1) },
                    { "player2", new AttributeValue(gameSession.player2) },
                }
            };

            var response = await dynamoDB.PutItemAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        public async Task<bool> UpdateGameSessionDataAsync(GameSession gameSession)
        {
            var request = new UpdateItemRequest
            {
                TableName = "game-session-1",
                Key = new Dictionary<string, AttributeValue>
                {
                    { "uuid", new AttributeValue(gameSession.uuid) }
                },

                UpdateExpression = "set player2 = :p2",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":p2", new AttributeValue(gameSession.player2) }
                }
            };

            var response = await dynamoDB.UpdateItemAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
    }
}