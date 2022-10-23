using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;

namespace game_messaging_2
{
    public class GameSessionDBOperations
    {
        private readonly IAmazonDynamoDB dynamoDB;

        public GameSessionDBOperations(IAmazonDynamoDB dynamoDB)
        {
            this.dynamoDB = dynamoDB;
        }
        
        public async Task<GameSession[]> GetGameSessionAsync(string playerId)
        {
            var result = await dynamoDB.ScanAsync(new ScanRequest
            {
                TableName = "game-session-1",
                FilterExpression = "#p1 = :playerId or #p2 = :playerId",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    {"#p1", "player1"},
                    {"#p2", "player2"}
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":playerId", new AttributeValue{ S = playerId}}
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
    }
}