namespace GoldSrc;

partial class Client
{
	private const int HltvActive = 0;
	private const int HltvStatus = 1;
	private const int HltvListen = 2;

	private bool _spectator;

	public bool IsSpectateOnly => _spectator;

	public int ConIsVisible => 0;

	private void ParseHltv( MessageReader message )
	{
		switch ( message.ReadByte() )
		{
			case HltvActive:
				_spectator = true;
				break;

			case HltvStatus:
				message.ReadLong();
				message.ReadShort();
				message.ReadWord();
				message.ReadLong();
				message.ReadLong();
				message.ReadWord();
				break;

			case HltvListen:
				_signon = 2;
				message.ReadString();
				break;

			default:
				Con.Printf( " CL_Parse_HLTV: unknown HLTV command.\n" );
				break;
		}
	}
}
