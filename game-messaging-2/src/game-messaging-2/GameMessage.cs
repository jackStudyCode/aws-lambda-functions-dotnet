using Newtonsoft.Json;

namespace game_messaging_2
{
    public class GameMessage
    {
        public string uuid;
        public string opcode;
        public string message;
        public string gameStatus;
        public string action;


        public GameMessage(string uuid, string opcode)
        {
            this.uuid = uuid;
            this.opcode = opcode;
        }

        public GameMessage(string uuid, string opcode, string message)
        {
            this.uuid = uuid;
            this.opcode = opcode;
            this.message = message;
        }

        [JsonConstructor]
        public GameMessage(string uuid, string opcode, string message, string gameStatus, string action)
        {
            this.uuid = uuid;
            this.opcode = opcode;
            this.message = message;
            this.gameStatus = gameStatus;
            this.action = action;
        }
    }
}