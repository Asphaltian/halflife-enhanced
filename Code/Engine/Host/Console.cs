using System.Text;

namespace GoldSrc;

public static class Con
{
	private static StringBuilder _line = new();

	public static void Printf( string text )
	{
		_line.Append( text );

		var end = text.LastIndexOf( '\n' );

		if ( end < 0 )
			return;

		var pending = _line.Length - (text.Length - end);

		Log.Info( _line.ToString( 0, pending ) );

		_line.Remove( 0, pending + 1 );
	}

	public static void DPrintf( string text )
	{
		if ( Host.Developer != 0f )
			Printf( text );
	}
}
