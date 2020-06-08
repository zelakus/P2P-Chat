public enum Opcode : byte
{
    Heartbeat,

    //Lobby-Client
    PresenceInf,
    LobbyInf,
    ConnectPeerInf,

    //Client-Client
    Hello,
    Message
}
