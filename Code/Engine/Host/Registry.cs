namespace GoldSrc;

public static class Registry
{
	public static int ReadInt( string key, int defaultValue = 0 ) => Game.Cookies.Get( key, defaultValue );

	public static void WriteInt( string key, int value ) => Game.Cookies.Set( key, value );

	public static string ReadString( string key, string defaultValue = "" ) => Game.Cookies.Get( key, defaultValue );

	public static void WriteString( string key, string value ) => Game.Cookies.Set( key, value );
}
