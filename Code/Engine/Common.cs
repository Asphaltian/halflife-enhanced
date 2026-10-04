using System;
using System.Globalization;
using System.Text;

namespace GoldSrc;

public static class Common
{
	private const int MaxToken = 2047;

	public static bool IgnoreColons { get; set; }

	public static string Parse( string data, ref int position )
	{
		while ( true )
		{
			while ( position < data.Length && data[position] <= ' ' )
				position++;

			if ( position >= data.Length )
				return null;

			if ( data[position] != '/' || position + 1 >= data.Length || data[position + 1] != '/' )
				break;

			while ( position < data.Length && data[position] != '\n' )
				position++;
		}

		var token = new StringBuilder();
		var c = data[position];

		if ( c == '"' )
		{
			position++;

			while ( position < data.Length )
			{
				c = data[position++];
				if ( c == '"' || token.Length >= MaxToken )
					break;

				token.Append( c );
			}

			return token.ToString();
		}

		if ( IsSingleCharacter( c ) || c == ',' )
		{
			position++;
			return c.ToString();
		}

		while ( true )
		{
			token.Append( c );
			position++;

			if ( position >= data.Length || token.Length >= MaxToken )
				break;

			c = data[position];
			if ( IsSingleCharacter( c ) || c == ',' || c <= ' ' )
				break;
		}

		return token.ToString();
	}

	private static bool IsSingleCharacter( char c )
	{
		return c is '{' or '}' or ')' or '(' or '\'' || (c == ':' && !IgnoreColons);
	}

	public static string MemFgets( string file, ref int position, int bufferSize )
	{
		if ( position >= file.Length )
			return null;

		var size = System.Math.Min( bufferSize - 1, file.Length - position );
		var newLine = file.IndexOf( '\n', position, size );
		var length = newLine < 0 ? size : newLine - position + 1;

		var line = file.Substring( position, length );
		position += length;

		return line;
	}

	public static int Atoi( string value )
	{
		var position = 0;

		while ( position < value.Length && char.IsWhiteSpace( value[position] ) )
			position++;

		var sign = 1;

		if ( position < value.Length && value[position] is '-' or '+' )
		{
			if ( value[position] == '-' )
				sign = -1;

			position++;
		}

		var result = 0;

		for ( ; position < value.Length && char.IsAsciiDigit( value[position] ); position++ )
			result = (result * 10) + (value[position] - '0');

		return sign * result;
	}

	public static double Strtod( string value )
	{
		var start = 0;

		while ( start < value.Length && char.IsWhiteSpace( value[start] ) )
			start++;

		var position = start;

		if ( Peek( value, position ) is '+' or '-' )
			position++;

		var digits = 0;

		for ( ; char.IsAsciiDigit( Peek( value, position ) ); position++ )
			digits++;

		if ( Peek( value, position ) == '.' )
		{
			for ( position++; char.IsAsciiDigit( Peek( value, position ) ); position++ )
				digits++;
		}

		if ( digits == 0 )
			return 0.0;

		if ( Peek( value, position ) is 'e' or 'E' )
		{
			var exponent = position + 1;

			if ( Peek( value, exponent ) is '+' or '-' )
				exponent++;

			if ( char.IsAsciiDigit( Peek( value, exponent ) ) )
			{
				position = exponent;

				while ( char.IsAsciiDigit( Peek( value, position ) ) )
					position++;
			}
		}

		return double.Parse( value.AsSpan( start, position - start ), NumberStyles.Float, CultureInfo.InvariantCulture );
	}

	public static float Atof( string value )
	{
		var position = 0;
		var sign = 1;

		if ( Peek( value, position ) == '-' )
		{
			sign = -1;
			position++;
		}

		var result = 0.0;

		if ( Peek( value, position ) == '0' && Peek( value, position + 1 ) is 'x' or 'X' )
		{
			position += 2;

			while ( true )
			{
				var c = Peek( value, position++ );

				if ( c is >= '0' and <= '9' )
					result = (result * 16) + c - '0';
				else if ( c is >= 'a' and <= 'f' )
					result = (result * 16) + c - 'a' + 10;
				else if ( c is >= 'A' and <= 'F' )
					result = (result * 16) + c - 'A' + 10;
				else
					return (float)(sign * result);
			}
		}

		if ( Peek( value, position ) == '\'' )
			return sign * Peek( value, position + 1 );

		var total = 0;
		var decimalPoint = -1;

		while ( true )
		{
			var c = Peek( value, position++ );

			if ( c is '.' or ',' )
			{
				decimalPoint = total;
				continue;
			}

			if ( c is < '0' or > '9' )
				break;

			result = (result * 10) + c - '0';
			total++;
		}

		if ( decimalPoint == -1 )
			return (float)(sign * result);

		for ( ; total > decimalPoint; total-- )
			result /= 10;

		return (float)(sign * result);
	}

	private static char Peek( string value, int position )
	{
		return position < value.Length ? value[position] : '\0';
	}

	public static string FileBase( string path )
	{
		if ( string.IsNullOrEmpty( path ) )
			return "";

		var end = path.Length - 1;

		while ( end != 0 && path[end] is not ('.' or '/' or '\\') )
			end--;

		end = path[end] == '.' ? end - 1 : path.Length - 1;

		var start = path.Length - 1;

		while ( start >= 0 && path[start] is not ('/' or '\\') )
			start--;

		start++;

		return end >= start ? path.Substring( start, end - start + 1 ) : "";
	}

	public static Vector3 NormalizeAngles( Vector3 angles )
	{
		for ( var i = 0; i < 3; i++ )
		{
			if ( angles[i] > 180f )
				angles[i] = (float)((float)(angles[i] % 360.0) - 360.0);
			else if ( angles[i] < -180f )
				angles[i] = (float)((float)(angles[i] % 360.0) + 360.0);
		}

		return angles;
	}

	private const int SequenceCrcTableSize = 0x3FC;
	private const int SequenceCrcMaxLength = 60;

	private static readonly byte[] MungifyTable = [0x7A, 0x64, 0x05, 0xF1, 0x1B, 0x9B, 0xA0, 0xB5, 0xCA, 0xED, 0x61, 0x0D, 0x4A, 0xDF, 0x8E, 0xC7];
	private static readonly byte[] MungifyTable2 = [0x05, 0x61, 0x7A, 0xED, 0x1B, 0xCA, 0x0D, 0x9B, 0x4A, 0xF1, 0x64, 0xC7, 0xB5, 0x8E, 0xDF, 0xA0];
	private static readonly byte[] MungifyTable3 = [0x20, 0x07, 0x13, 0x61, 0x03, 0x45, 0x17, 0x72, 0x0A, 0x2D, 0x48, 0x0C, 0x4A, 0x12, 0xA9, 0xB5];

	public static byte BlockSequenceCrcByte( ReadOnlySpan<byte> data, int sequence )
	{
		if ( sequence < 0 )
			throw new InvalidOperationException( "sequence < 0, in COM_BlockSequenceCRCByte\n" );

		var offset = sequence % SequenceCrcTableSize;
		var length = Math.Min( data.Length, SequenceCrcMaxLength );

		Span<byte> buffer = stackalloc byte[SequenceCrcMaxLength + 4];

		data[..length].CopyTo( buffer );

		for ( var i = 0; i < 4; i++ )
			buffer[length + i] = Crc32.TableByte( offset + i );

		return (byte)Crc32.Compute( buffer[..(length + 4)] );
	}

	private static uint LongSwap( uint value )
	{
		return ((value & 0xFF) << 24) | ((value & 0xFF00) << 8) | ((value >> 8) & 0xFF00) | (value >> 24);
	}

	private static uint MungeBytes( byte[] table, uint value, int index )
	{
		for ( var j = 0; j < 4; j++ )
			value ^= (uint)((byte)(0xA5 | (j << j) | j | table[(index + j) & 0x0F]) << (j * 8));

		return value;
	}

	private static void Munge( byte[] table, Span<byte> data, int sequence )
	{
		var loops = data.Length / 4;

		for ( var i = 0; i < loops; i++ )
		{
			var word = data.Slice( i * 4, 4 );
			var value = BitConverter.ToUInt32( word ) ^ ~(uint)sequence;

			value = MungeBytes( table, LongSwap( value ), i ) ^ (uint)sequence;

			BitConverter.TryWriteBytes( word, value );
		}
	}

	private static void UnMunge( byte[] table, Span<byte> data, int sequence )
	{
		var loops = data.Length / 4;

		for ( var i = 0; i < loops; i++ )
		{
			var word = data.Slice( i * 4, 4 );
			var value = MungeBytes( table, BitConverter.ToUInt32( word ) ^ (uint)sequence, i );

			value = LongSwap( value ) ^ ~(uint)sequence;

			BitConverter.TryWriteBytes( word, value );
		}
	}

	public static void Munge( Span<byte> data, int sequence ) => Munge( MungifyTable, data, sequence );

	public static void UnMunge( Span<byte> data, int sequence ) => UnMunge( MungifyTable, data, sequence );

	public static void Munge2( Span<byte> data, int sequence ) => Munge( MungifyTable2, data, sequence );

	public static void UnMunge2( Span<byte> data, int sequence ) => UnMunge( MungifyTable2, data, sequence );

	public static int Munge2( int value, int sequence ) => MungeLong( MungifyTable2, value, sequence );

	public static int UnMunge2( int value, int sequence ) => UnMungeLong( MungifyTable2, value, sequence );

	public static int Munge3( int value, int sequence ) => MungeLong( MungifyTable3, value, sequence );

	public static int UnMunge3( int value, int sequence ) => UnMungeLong( MungifyTable3, value, sequence );

	private static int MungeLong( byte[] table, int value, int sequence )
	{
		Span<byte> data = stackalloc byte[4];

		BitConverter.TryWriteBytes( data, value );

		Munge( table, data, sequence );

		return BitConverter.ToInt32( data );
	}

	private static int UnMungeLong( byte[] table, int value, int sequence )
	{
		Span<byte> data = stackalloc byte[4];

		BitConverter.TryWriteBytes( data, value );

		UnMunge( table, data, sequence );

		return BitConverter.ToInt32( data );
	}
}
