namespace GoldSrc;

public sealed class ServerNetwork : Component, Component.INetworkListener
{
	public void OnDisconnected( Connection connection )
	{
		Server.Get( Scene ).DisconnectClient( connection );
	}
}
