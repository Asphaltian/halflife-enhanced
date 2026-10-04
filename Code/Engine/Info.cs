using System;
using System.Text;

namespace GoldSrc;

public static class Unicode
{
	private static bool IsValid( uint value )
	{
		return value < 0x110000u && (value - 0xD800u) > 0x7FFu && (value & 0xFFFFu) < 0xFFFEu && (value - 0xFDD0u) > 0x1Fu;
	}

	public static int Decode( string text, int position, out uint value, out bool error )
	{
		uint At( int index ) => position + index < text.Length ? text[position + index] : 0u;

		value = At( 0 );
		error = false;

		if ( value < 0x80 )
			return 1;

		var bytes = 1;
		var minimum = 0u;
		var valid = false;

		if ( (value - 0xC0u) <= 0x37u && (At( 1 ) & 0xC0) == 0x80 )
		{
			bytes = 2;
			value = (value << 6) - 0x3080 + At( 1 );
			minimum = 0x80;
			valid = true;

			if ( (value & 0x800) != 0 )
			{
				if ( (At( 2 ) & 0xC0) != 0x80 )
				{
					valid = false;
				}
				else
				{
					bytes = 3;
					value = (value << 6) + At( 2 ) - 0x20080;
					minimum = 0x800;

					if ( (value & 0x10000) != 0 )
					{
						if ( (At( 3 ) & 0xC0) != 0x80 )
						{
							valid = false;
						}
						else
						{
							bytes = 4;
							value = (value << 6) + At( 3 ) - 0x400080;
							minimum = 0x10000;
						}
					}
					else if ( (value - 0xD800u) < 0x400u && At( 3 ) == 0xED && (byte)(At( 4 ) + 0x50) < 0x10 && (At( 5 ) & 0xC0) == 0x80 )
					{
						bytes = 6;
						value = (((uint)(byte)(At( 4 ) + 0x50) + ((value - 0xD800u) << 4) + 0x3FE) << 6) + At( 5 );
						minimum = 0x10000;
					}
				}
			}
		}

		if ( valid && value >= minimum && IsValid( value ) )
			return bytes;

		value = '?';
		error = true;

		return bytes;
	}

	private static void Encode( StringBuilder builder, uint value )
	{
		if ( value < 0x80 )
		{
			builder.Append( (char)value );
			return;
		}

		var last = (char)((value & 0x3F) | 0x80);

		if ( value < 0x800 )
		{
			builder.Append( (char)((value >> 6) | 0xC0) ).Append( last );
			return;
		}

		var middle = (char)(((value >> 6) & 0x3F) | 0x80);

		if ( value < 0x10000 )
		{
			builder.Append( (char)((value >> 12) | 0xE0) ).Append( middle ).Append( last );
			return;
		}

		builder.Append( (char)(((value >> 18) & 7) | 0xF0) ).Append( (char)(((value >> 12) & 0x3F) | 0x80) ).Append( middle ).Append( last );
	}

	public static bool Validate( string text )
	{
		for ( var position = 0; position < text.Length; )
		{
			var size = Decode( text, position, out _, out var error );

			if ( error || size == 6 )
				return false;

			position += size;
		}

		return true;
	}

	public static string Repair( string text )
	{
		var builder = new StringBuilder();

		for ( var position = 0; position < text.Length; )
		{
			position += Decode( text, position, out var value, out var error );

			if ( !error )
				Encode( builder, value );
		}

		return builder.ToString();
	}
}

public static class Info
{
	private const int MaxKeyValueLength = 127;

	private static readonly string[] ImportantKeys = ["name", "model", "rate", "topcolor", "bottomcolor", "cl_updaterate", "cl_lw", "cl_lc", "*hltv", "*sid"];

	private static bool Next( string info, ref int position, out string key, out string value )
	{
		char At( int index ) => index < info.Length ? info[index] : '\0';

		var builder = new StringBuilder();

		for ( var count = 0; At( position ) != '\\' && count < MaxKeyValueLength; count++ )
		{
			if ( At( position ) == '\0' )
			{
				key = value = null;
				return false;
			}

			builder.Append( info[position++] );
		}

		position++;
		key = builder.ToString();

		builder.Clear();

		for ( var count = 0; At( position ) != '\\' && At( position ) != '\0' && count < MaxKeyValueLength; count++ )
			builder.Append( info[position++] );

		value = builder.ToString();

		return true;
	}

	public static string ValueForKey( string info, string key )
	{
		var position = info.StartsWith( '\\' ) ? 1 : 0;

		while ( Next( info, ref position, out var name, out var value ) )
		{
			if ( name == key )
				return value;

			if ( position >= info.Length )
				break;

			position++;
		}

		return "";
	}

	public static void Print( string info )
	{
		var position = info.StartsWith( '\\' ) ? 1 : 0;

		while ( position < info.Length )
		{
			var start = position;

			while ( position < info.Length && info[position] != '\\' && position - start < MaxKeyValueLength )
				position++;

			var key = info[start..position].PadRight( 20 );

			if ( position >= info.Length )
			{
				Log.Info( $"{key}MISSING VALUE" );
				return;
			}

			start = ++position;

			while ( position < info.Length && info[position] != '\\' && position - start < MaxKeyValueLength )
				position++;

			Log.Info( $"{key}{info[start..position]}" );

			if ( position < info.Length )
				position++;
		}
	}

	public static void RemovePrefixedKeys( ref string info, char prefix )
	{
		var position = info.StartsWith( '\\' ) ? 1 : 0;

		while ( Next( info, ref position, out var key, out _ ) )
		{
			if ( key.Length > 0 && key[0] == prefix )
			{
				RemoveKey( ref info, key );

				position = info.StartsWith( '\\' ) ? 1 : 0;

				continue;
			}

			if ( position >= info.Length )
				break;

			position++;
		}
	}

	public static void RemoveKey( ref string info, string key )
	{
		if ( key.Contains( '\\' ) )
		{
			Log.Info( "Can't use a key with a \\" );
			return;
		}

		var prefix = key.Length < MaxKeyValueLength ? key : key[..MaxKeyValueLength];
		var start = 0;

		while ( start < info.Length )
		{
			var position = info[start] == '\\' ? start + 1 : start;

			if ( !Next( info, ref position, out var name, out _ ) )
				return;

			if ( name.StartsWith( prefix, StringComparison.Ordinal ) )
			{
				info = info[..start] + (position < info.Length ? info[position..] : "");
				return;
			}

			start = position;
		}
	}

	private static bool IsKeyImportant( string key ) => key.StartsWith( '*' ) || Array.IndexOf( ImportantKeys, key ) >= 0;

	private static string FindLargestKey( string info )
	{
		var largest = "";
		var size = 0;
		var position = info.StartsWith( '\\' ) ? 1 : 0;

		while ( position < info.Length )
		{
			if ( !Next( info, ref position, out var key, out var value ) )
				break;

			if ( position < info.Length )
				position++;

			if ( size < key.Length + value.Length && !IsKeyImportant( key ) )
			{
				largest = key;
				size = key.Length + value.Length;
			}
		}

		return largest;
	}

	public static void SetValueForKey( ref string info, string key, string value, int maxSize )
	{
		if ( key.StartsWith( '*' ) )
		{
			Log.Info( "Can't set * keys" );
			return;
		}

		SetValueForStarKey( ref info, key, value, maxSize );
	}

	public static void SetValueForStarKey( ref string info, string key, string value, int maxSize )
	{
		if ( key.Contains( '\\' ) || value.Contains( '\\' ) )
		{
			Log.Info( "Can't use keys or values with a \\" );
			return;
		}

		if ( key.Contains( ".." ) || value.Contains( ".." ) )
			return;

		if ( key.Contains( '"' ) || value.Contains( '"' ) )
		{
			Log.Info( "Can't use keys or values with a \"" );
			return;
		}

		if ( key.Length < 1 || key.Length > MaxKeyValueLength || value.Length > MaxKeyValueLength )
		{
			Log.Info( $"Keys and values must be < {MaxKeyValueLength + 1} characters and > 0." );
			return;
		}

		if ( !Unicode.Validate( value ) )
		{
			Log.Info( "Values must be valid utf8 text" );
			return;
		}

		while ( ValueForKey( info, key ).Length != 0 )
			RemoveKey( ref info, key );

		if ( value.Length == 0 )
			return;

		var pair = $"\\{key}\\{value}";

		if ( info.Length + pair.Length >= maxSize )
		{
			if ( !IsKeyImportant( key ) )
			{
				Log.Info( "Info string length exceeded" );
				return;
			}

			string largest;

			do
			{
				largest = FindLargestKey( info );

				RemoveKey( ref info, largest );
			}
			while ( info.Length + pair.Length >= maxSize && largest.Length != 0 );

			if ( largest.Length == 0 )
			{
				Log.Info( "Info string length exceeded" );
				return;
			}
		}

		info += key.Equals( "team", StringComparison.OrdinalIgnoreCase ) ? pair.ToLowerInvariant() : pair;
	}
}
