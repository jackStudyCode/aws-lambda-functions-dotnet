namespace disconnect_game_2
{
    public class GameMessage
    {
        public string uuid;
        public string opcode;

        public GameMessage(string uuid, string opcode)
        {
            this.uuid = uuid;
            this.opcode = opcode;
        }
    }
}