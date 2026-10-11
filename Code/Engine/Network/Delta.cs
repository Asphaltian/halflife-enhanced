using System;
using System.IO;

namespace GoldSrc;

public interface IDeltaEncoder
{
	void Encode( Delta fields, byte[] from, byte[] to );
}

public sealed class DeltaField
{
	public int Type;
	public string Name;
	public int Offset;
	public int FieldSize;
	public int SignificantBits;
	public float PreMultiply;
	public float PostMultiply;
	public int Flags;
	public int SendCount;
	public int ReceivedCount;
}

public sealed class Delta
{
	public const int TypeByte = 1 << 0;
	public const int TypeShort = 1 << 1;
	public const int TypeFloat = 1 << 2;
	public const int TypeInteger = 1 << 3;
	public const int TypeAngle = 1 << 4;
	public const int TypeTimeWindow8 = 1 << 5;
	public const int TypeTimeWindowBig = 1 << 6;
	public const int TypeString = 1 << 7;
	public const int TypeSigned = int.MinValue;

	public const int FlagMark = 1 << 0;

	private const int MaxNameLength = 31;
	private const float MultiplierLow = 0.9999f;
	private const float MultiplierHigh = 1.0001f;

	private static (string Name, int Offset)[] ClientDataFields =
	[
		("origin[0]", 0), ("origin[1]", 4), ("origin[2]", 8), ("velocity[0]", 12), ("velocity[1]", 16), ("velocity[2]", 20),
		("viewmodel", 24), ("punchangle[0]", 28), ("punchangle[1]", 32), ("punchangle[2]", 36), ("flags", 40), ("waterlevel", 44),
		("watertype", 48), ("view_ofs[0]", 52), ("view_ofs[1]", 56), ("view_ofs[2]", 60), ("health", 64), ("bInDuck", 68),
		("weapons", 72), ("flTimeStepSound", 76), ("flDuckTime", 80), ("flSwimTime", 84), ("waterjumptime", 88), ("maxspeed", 92),
		("m_iId", 104), ("ammo_nails", 112), ("ammo_shells", 108), ("ammo_cells", 116), ("ammo_rockets", 120), ("m_flNextAttack", 124),
		("physinfo", 140), ("fov", 96), ("weaponanim", 100), ("tfstate", 128), ("pushmsec", 132), ("deadflag", 136),
		("iuser1", 396), ("iuser2", 400), ("iuser3", 404), ("iuser4", 408), ("fuser1", 412), ("fuser2", 416), ("fuser3", 420), ("fuser4", 424),
		("vuser1[0]", 428), ("vuser1[1]", 432), ("vuser1[2]", 436), ("vuser2[0]", 440), ("vuser2[1]", 444), ("vuser2[2]", 448),
		("vuser3[0]", 452), ("vuser3[1]", 456), ("vuser3[2]", 460), ("vuser4[0]", 464), ("vuser4[1]", 468), ("vuser4[2]", 472)
	];

	private static (string Name, int Offset)[] WeaponDataFields =
	[
		("m_iId", 0), ("m_iClip", 4), ("m_flNextPrimaryAttack", 8), ("m_flNextSecondaryAttack", 12), ("m_flTimeWeaponIdle", 16),
		("m_fInReload", 20), ("m_fInSpecialReload", 24), ("m_flNextReload", 28), ("m_flPumpTime", 32), ("m_fReloadTime", 36),
		("m_fAimedDamage", 40), ("m_fNextAimBonus", 44), ("m_fInZoom", 48), ("m_iWeaponState", 52),
		("iuser1", 56), ("iuser2", 60), ("iuser3", 64), ("iuser4", 68), ("fuser1", 72), ("fuser2", 76), ("fuser3", 80), ("fuser4", 84)
	];

	private static (string Name, int Offset)[] UserCmdFields =
	[
		("lerp_msec", 0), ("msec", 2), ("lightlevel", 28), ("viewangles[0]", 4), ("viewangles[1]", 8), ("viewangles[2]", 12),
		("buttons", 30), ("forwardmove", 16), ("sidemove", 20), ("upmove", 24), ("impulse", 32), ("weaponselect", 33),
		("impact_index", 36), ("impact_position[0]", 40), ("impact_position[1]", 44), ("impact_position[2]", 48)
	];

	private static (string Name, int Offset)[] EntityStateFields =
	[
		("startpos[0]", 228), ("startpos[1]", 232), ("startpos[2]", 236), ("endpos[0]", 240), ("endpos[1]", 244), ("endpos[2]", 248),
		("impacttime", 252), ("starttime", 256), ("origin[0]", 16), ("origin[1]", 20), ("origin[2]", 24),
		("angles[0]", 28), ("angles[1]", 32), ("angles[2]", 36), ("modelindex", 40), ("frame", 48), ("movetype", 88), ("colormap", 52),
		("skin", 56), ("solid", 58), ("scale", 64), ("effects", 60), ("sequence", 44), ("animtime", 92), ("framerate", 96),
		("controller[0]", 104), ("controller[1]", 105), ("controller[2]", 106), ("controller[3]", 107), ("blending[0]", 108), ("blending[1]", 109),
		("body", 100), ("owner", 152), ("rendermode", 72), ("renderamt", 76), ("renderfx", 84),
		("rendercolor.r", 80), ("rendercolor.g", 81), ("rendercolor.b", 82), ("weaponmodel", 180), ("gaitsequence", 184),
		("mins[0]", 124), ("mins[1]", 128), ("mins[2]", 132), ("maxs[0]", 136), ("maxs[1]", 140), ("maxs[2]", 144), ("aiment", 148),
		("basevelocity[0]", 188), ("basevelocity[1]", 192), ("basevelocity[2]", 196), ("friction", 156), ("gravity", 160), ("spectator", 176),
		("velocity[0]", 112), ("velocity[1]", 116), ("velocity[2]", 120), ("team", 164), ("playerclass", 168), ("health", 172),
		("usehull", 200), ("oldbuttons", 204), ("onground", 208), ("iStepLeft", 212), ("flFallVelocity", 216), ("weaponanim", 224), ("eflags", 68),
		("iuser1", 260), ("iuser2", 264), ("iuser3", 268), ("iuser4", 272), ("fuser1", 276), ("fuser2", 280), ("fuser3", 284), ("fuser4", 288),
		("vuser1[0]", 292), ("vuser1[1]", 296), ("vuser1[2]", 300), ("vuser2[0]", 304), ("vuser2[1]", 308), ("vuser2[2]", 312),
		("vuser3[0]", 316), ("vuser3[1]", 320), ("vuser3[2]", 324), ("vuser4[0]", 328), ("vuser4[1]", 332), ("vuser4[2]", 336)
	];

	private static (string Name, int Offset)[] EventFields =
	[
		("entindex", 4), ("origin[0]", 8), ("origin[1]", 12), ("origin[2]", 16), ("angles[0]", 20), ("angles[1]", 24), ("angles[2]", 28),
		("fparam1", 48), ("fparam2", 52), ("iparam1", 56), ("iparam2", 60), ("bparam1", 64), ("bparam2", 68), ("ducking", 44)
	];

	private const int DescriptionSize = 0x44;
	private const int DescriptionNameSize = 32;

	private static readonly string[] DescriptionNames =
	[
		"clientdata_t", "entity_state_t", "entity_state_player_t", "custom_entity_state_t", "usercmd_t", "weapon_data_t", "event_t"
	];

	private static Delta Meta = new()
	{
		Fields =
		[
			new DeltaField { Type = TypeInteger, Name = "fieldType", Offset = 0, FieldSize = 1, SignificantBits = 32, PreMultiply = 1f, PostMultiply = 1f },
			new DeltaField { Type = TypeString, Name = "fieldName", Offset = 4, FieldSize = 1, SignificantBits = 1, PreMultiply = 1f, PostMultiply = 1f },
			new DeltaField { Type = TypeInteger, Name = "fieldOffset", Offset = 36, FieldSize = 1, SignificantBits = 16, PreMultiply = 1f, PostMultiply = 1f },
			new DeltaField { Type = TypeInteger, Name = "fieldSize", Offset = 40, FieldSize = 1, SignificantBits = 8, PreMultiply = 1f, PostMultiply = 1f },
			new DeltaField { Type = TypeInteger, Name = "significant_bits", Offset = 44, FieldSize = 1, SignificantBits = 8, PreMultiply = 1f, PostMultiply = 1f },
			new DeltaField { Type = TypeFloat, Name = "premultiply", Offset = 48, FieldSize = 1, SignificantBits = 32, PreMultiply = 4000f, PostMultiply = 1f },
			new DeltaField { Type = TypeFloat, Name = "postmultiply", Offset = 52, FieldSize = 1, SignificantBits = 32, PreMultiply = 4000f, PostMultiply = 1f }
		]
	};

	private static List<(string Name, IDeltaEncoder Encoder)> Encoders = [];
	private static List<(string Name, Delta Delta)> Registry = [];

	public DeltaField[] Fields { get; private set; } = [];

	public string ConditionalEncodeName { get; private set; } = "";

	public IDeltaEncoder ConditionalEncode { get; set; }

	private static (string Name, int Offset)[] FindDefinition( string name )
	{
		return name.ToLowerInvariant() switch
		{
			"clientdata_t" => ClientDataFields,
			"weapon_data_t" => WeaponDataFields,
			"usercmd_t" => UserCmdFields,
			"entity_state_t" or "entity_state_player_t" or "custom_entity_state_t" => EntityStateFields,
			"event_t" => EventFields,
			_ => null
		};
	}

	public static void AddEncoder( string name, IDeltaEncoder encoder )
	{
		Encoders.Add( (name, encoder) );
	}

	public static void ClearEncoders() => Encoders.Clear();

	public static IDeltaEncoder LookupEncoder( string name )
	{
		foreach ( var (encoderName, encoder) in Encoders )
		{
			if ( encoderName.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return encoder;
		}

		return null;
	}

	public static void InitRegistry()
	{
		Registry.Clear();

		foreach ( var name in DescriptionNames )
			Registry.Insert( 0, (name, null) );
	}

	public static void SetRegistration( string name, Delta delta )
	{
		var index = Registry.FindIndex( entry => entry.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) );

		Registry[index] = (Registry[index].Name, delta);
	}

	public void WriteDescription( SizeBuf buffer )
	{
		var image = new byte[DescriptionSize];
		var zero = new byte[DescriptionSize];

		buffer.WriteBits( (uint)Fields.Length, 16 );

		foreach ( var field in Fields )
		{
			Array.Clear( image );

			StructImage.Write( image, 0, field.Type );
			StructImage.Write( image, 4, DescriptionNameSize, field.Name );
			StructImage.Write( image, 36, field.Offset );
			StructImage.Write( image, 40, (short)field.FieldSize );
			StructImage.Write( image, 44, field.SignificantBits );
			StructImage.Write( image, 48, field.PreMultiply );
			StructImage.Write( image, 52, field.PostMultiply );

			Meta.WriteDelta( buffer, zero, image, true, 0.0 );
		}
	}

	public static Delta ReadDescription( MessageReader reader )
	{
		var image = new byte[DescriptionSize];
		var zero = new byte[DescriptionSize];
		var fields = new DeltaField[(int)reader.ReadBits( 16 )];

		for ( var i = 0; i < fields.Length; i++ )
		{
			Array.Clear( image );

			Meta.ParseDelta( reader, zero, image, 0.0 );

			fields[i] = new DeltaField
			{
				Type = StructImage.ReadInt( image, 0 ),
				Name = StructImage.ReadString( image, 4, DescriptionNameSize ),
				Offset = StructImage.ReadInt( image, 36 ),
				FieldSize = StructImage.ReadShort( image, 40 ),
				SignificantBits = StructImage.ReadInt( image, 44 ),
				PreMultiply = StructImage.ReadFloat( image, 48 ),
				PostMultiply = StructImage.ReadFloat( image, 52 )
			};
		}

		return new Delta { Fields = fields };
	}

	public static Delta LookupRegistration( string name )
	{
		foreach ( var (registeredName, delta) in Registry )
		{
			if ( registeredName.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return delta;
		}

		return null;
	}

	public static Delta Load( string name, string fileName )
	{
		if ( Server.LoadMountText( fileName ) is not { } file )
			throw new InvalidDataException( $"DELTA_Load:  Couldn't load file {fileName}\n" );

		return ParseDescription( name, file );
	}

	private sealed class Tokens( string data )
	{
		private int _position;
		private bool _unget;

		public string Token { get; private set; } = "";

		public void Next()
		{
			if ( _unget )
			{
				_unget = false;
				return;
			}

			Token = Common.Parse( data, ref _position ) ?? "";
		}

		public void Unget() => _unget = true;

		public bool Is( string value ) => Token.Equals( value, StringComparison.OrdinalIgnoreCase );
	}

	private static string ClampName( string name ) => name.Length > MaxNameLength ? name[..MaxNameLength] : name;

	public static Delta ParseDescription( string name, string data )
	{
		if ( data is null )
			throw new InvalidDataException( "DELTA_ParseDescription with no data stream\n" );

		var fields = new List<DeltaField>();
		var encoderName = "";
		var tokens = new Tokens( data );

		tokens.Next();

		while ( tokens.Token.Length > 0 )
		{
			if ( !tokens.Is( name ) )
			{
				tokens.Next();
				tokens.Next();

				while ( true )
				{
					if ( tokens.Token.Length < 1 )
						throw new InvalidDataException( "Error during description skip" );

					if ( tokens.Is( "}" ) )
						break;

					tokens.Next();
				}

				tokens.Next();
				continue;
			}

			var definition = FindDefinition( tokens.Token );

			if ( definition is null )
				throw new InvalidDataException( $"DELTA_ParseDescription:  Unknown data type:  {tokens.Token}\n" );

			tokens.Next();

			if ( tokens.Token.Length < 1 )
				throw new InvalidDataException( $"DELTA_ParseDescription:  Unknown encoder :  {tokens.Token}\nValid values:\nnone\ngamedll funcname\nclientdll funcname\n" );

			if ( !tokens.Is( "none" ) )
			{
				tokens.Next();

				if ( tokens.Token.Length < 1 )
					throw new InvalidDataException( "DELTA_ParseDescription:  Expecting encoder\n" );

				encoderName = ClampName( tokens.Token );
			}

			tokens.Next();

			while ( tokens.Token.Length > 0 && !tokens.Is( "}" ) )
			{
				if ( !tokens.Is( "{" ) )
				{
					Log.Info( $"DELTA_ParseDescription:  Expecting {{, got {tokens.Token}\n" );
					return null;
				}

				var complete = true;

				while ( !tokens.Is( "}" ) )
				{
					tokens.Next();

					if ( tokens.Token.Length < 1 )
					{
						complete = false;
						break;
					}

					var field = new DeltaField();

					if ( !ParseField( definition, field, tokens ) )
						return null;

					fields.Add( field );
				}

				if ( complete )
					tokens.Unget();

				tokens.Next();
			}

			tokens.Next();
		}

		return new Delta { Fields = [.. fields], ConditionalEncodeName = encoderName };
	}

	private static bool ParseField( (string Name, int Offset)[] definition, DeltaField field, Tokens tokens )
	{
		var post = false;

		if ( !tokens.Is( "DEFINE_DELTA" ) )
		{
			if ( !tokens.Is( "DEFINE_DELTA_POST" ) )
				throw new InvalidDataException( $"DELTA_ParseField:  Expecting DEFINE_*, got {tokens.Token}\n" );

			post = true;
		}

		tokens.Next();

		if ( !tokens.Is( "(" ) )
			throw new InvalidDataException( $"DELTA_ParseField:  Expecting (, got {tokens.Token}\n" );

		tokens.Next();

		if ( tokens.Token.Length < 1 )
			throw new InvalidDataException( "DELTA_ParseField:  Expecting fieldname\n" );

		field.Name = ClampName( tokens.Token );

		var found = false;

		foreach ( var (name, offset) in definition )
		{
			if ( !tokens.Is( name ) )
				continue;

			field.Offset = offset;
			found = true;
			break;
		}

		if ( !found )
			throw new InvalidDataException( $"Couldn't find offset for {tokens.Token}!!!\n" );

		tokens.Next();
		tokens.Next();

		while ( true )
		{
			if ( tokens.Token.Length < 1 )
			{
				Log.Info( "DELTA_ParseField:  Expecting fieldtype info\n" );
				return false;
			}

			if ( tokens.Is( "," ) )
				break;

			if ( !tokens.Is( "|" ) )
			{
				if ( tokens.Is( "DT_SIGNED" ) )
					field.Type |= TypeSigned;
				else if ( tokens.Is( "DT_BYTE" ) )
					field.Type |= TypeByte;
				else if ( tokens.Is( "DT_SHORT" ) )
					field.Type |= TypeShort;
				else if ( tokens.Is( "DT_FLOAT" ) )
					field.Type |= TypeFloat;
				else if ( tokens.Is( "DT_INTEGER" ) )
					field.Type |= TypeInteger;
				else if ( tokens.Is( "DT_ANGLE" ) )
					field.Type |= TypeAngle;
				else if ( tokens.Is( "DT_TIMEWINDOW_8" ) )
					field.Type |= TypeTimeWindow8;
				else if ( tokens.Is( "DT_TIMEWINDOW_BIG" ) )
					field.Type |= TypeTimeWindowBig;
				else if ( tokens.Is( "DT_STRING" ) )
					field.Type |= TypeString;
				else
					throw new InvalidDataException( $"DELTA_ParseField:  Unknown type or type flag {tokens.Token}\n" );
			}

			tokens.Next();
		}

		tokens.Next();

		field.FieldSize = 1;
		field.SignificantBits = Common.Atoi( tokens.Token );

		tokens.Next();
		tokens.Next();

		field.PreMultiply = Common.Atof( tokens.Token );

		if ( post )
		{
			tokens.Next();
			tokens.Next();

			field.PostMultiply = (float)Common.Strtod( tokens.Token );
		}
		else
		{
			field.PostMultiply = 1f;
		}

		tokens.Next();

		if ( !tokens.Is( ")" ) )
		{
			Log.Info( $"DELTA_ParseField:  Expecting ), got {tokens.Token}\n" );
			return false;
		}

		tokens.Next();

		if ( !tokens.Is( "," ) )
			tokens.Unget();

		return true;
	}

	public DeltaField FindField( string name )
	{
		foreach ( var field in Fields )
		{
			if ( field.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return field;
		}

		Log.Info( $"DELTA_FindField:  Warning, couldn't find {name}\n" );

		return null;
	}

	public int FindFieldIndex( string name )
	{
		for ( var i = 0; i < Fields.Length; i++ )
		{
			if ( Fields[i].Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		Log.Info( $"DELTA_FindFieldIndex:  Warning, couldn't find {name}\n" );

		return -1;
	}

	public void SetField( string name )
	{
		if ( FindField( name ) is { } field )
			field.Flags |= FlagMark;
	}

	public void UnsetField( string name )
	{
		if ( FindField( name ) is { } field )
			field.Flags &= ~FlagMark;
	}

	public void SetFieldByIndex( int index ) => Fields[index].Flags |= FlagMark;

	public void UnsetFieldByIndex( int index ) => Fields[index].Flags &= ~FlagMark;

	private static int Ftol( float value )
	{
		return value >= -2147483648f && value < 2147483648f ? (int)value : int.MinValue;
	}

	private static int Ftol( double value )
	{
		return value >= -2147483648.0 && value < 2147483648.0 ? (int)value : int.MinValue;
	}

	private static int FtolWide( float value )
	{
		return value >= -9223372036854775808f && value < 9223372036854775808f ? unchecked((int)(long)value) : 0;
	}

	private static bool Multiplies( float multiplier ) => multiplier < MultiplierLow || multiplier > MultiplierHigh;

	private static int StringLength( byte[] image, int offset )
	{
		var length = 0;

		while ( offset + length < image.Length && image[offset + length] != 0 )
			length++;

		return length;
	}

	private static bool StringsDiffer( byte[] from, byte[] to, int offset )
	{
		var fromLength = StringLength( from, offset );
		var toLength = StringLength( to, offset );

		if ( fromLength != toLength )
			return true;

		for ( var i = 0; i < fromLength; i++ )
		{
			if ( char.ToLowerInvariant( (char)from[offset + i] ) != char.ToLowerInvariant( (char)to[offset + i] ) )
				return true;
		}

		return false;
	}

	private void MarkSendFields( byte[] from, byte[] to )
	{
		foreach ( var field in Fields )
		{
			var offset = field.Offset;
			bool changed;

			switch ( field.Type & int.MaxValue )
			{
				case TypeByte:
					changed = from[offset] != to[offset];
					break;

				case TypeShort:
					changed = StructImage.ReadShort( from, offset ) != StructImage.ReadShort( to, offset );
					break;

				case TypeFloat:
				case TypeInteger:
				case TypeAngle:
					changed = StructImage.ReadInt( from, offset ) != StructImage.ReadInt( to, offset );
					break;

				case TypeTimeWindow8:
					changed = Ftol( StructImage.ReadFloat( from, offset ) * 100.0 ) != Ftol( StructImage.ReadFloat( to, offset ) * 100.0 );
					break;

				case TypeTimeWindowBig:
					changed = Ftol( StructImage.ReadFloat( from, offset ) * 1000.0 ) != Ftol( StructImage.ReadFloat( to, offset ) * 1000.0 );
					break;

				case TypeString:
					changed = StringsDiffer( from, to, offset );
					break;

				default:
					Log.Info( $"Bad field type {field.Type & int.MaxValue}\n" );
					changed = false;
					break;
			}

			if ( changed )
				field.Flags |= FlagMark;
		}

		ConditionalEncode?.Encode( this, from, to );
	}

	private int CountSendFields()
	{
		var count = 0;

		foreach ( var field in Fields )
		{
			if ( (field.Flags & FlagMark) == 0 )
				continue;

			field.SendCount++;
			count++;
		}

		return count;
	}

	public int CheckDelta( byte[] from, byte[] to )
	{
		foreach ( var field in Fields )
			field.Flags = 0;

		MarkSendFields( from, to );

		return CountSendFields();
	}

	public bool WriteDelta( SizeBuf buffer, byte[] from, byte[] to, bool force, double time, Action callback = null )
	{
		var count = CheckDelta( from, to );

		if ( count == 0 && !force )
			return true;

		var bits = new byte[8];
		var lastBit = -1;

		for ( var i = Fields.Length - 1; i >= 0; i-- )
		{
			if ( (Fields[i].Flags & FlagMark) == 0 )
				continue;

			bits[i >> 3] |= (byte)(1 << (i & 7));

			if ( lastBit == -1 )
				lastBit = i;
		}

		var byteCount = (lastBit >> 3) + 1;

		callback?.Invoke();

		buffer.WriteBits( (uint)byteCount, 3 );

		for ( var i = 0; i < byteCount; i++ )
			buffer.WriteBits( bits[i], 8 );

		WriteMarkedFields( buffer, to, time );

		return true;
	}

	private void WriteMarkedFields( SizeBuf buffer, byte[] to, double time )
	{
		foreach ( var field in Fields )
		{
			if ( (field.Flags & FlagMark) == 0 )
				continue;

			var offset = field.Offset;
			var signed = field.Type < 0;
			var bits = field.SignificantBits;

			switch ( field.Type & int.MaxValue )
			{
				case TypeByte:
					if ( signed )
						buffer.WriteSBits( (sbyte)Ftol( (sbyte)to[offset] * field.PreMultiply ), bits );
					else
						buffer.WriteBits( (byte)Ftol( to[offset] * field.PreMultiply ), bits );

					break;

				case TypeShort:
					if ( signed )
						buffer.WriteSBits( (short)Ftol( StructImage.ReadShort( to, offset ) * field.PreMultiply ), bits );
					else
						buffer.WriteBits( (ushort)Ftol( (ushort)StructImage.ReadShort( to, offset ) * field.PreMultiply ), bits );

					break;

				case TypeFloat:
					{
						var value = Ftol( StructImage.ReadFloat( to, offset ) * field.PreMultiply );

						if ( signed )
							buffer.WriteSBits( value, bits );
						else
							buffer.WriteBits( (uint)value, bits );

						break;
					}

				case TypeInteger:
					{
						var value = StructImage.ReadInt( to, offset );

						if ( signed )
						{
							if ( Multiplies( field.PreMultiply ) )
								value = Ftol( value * field.PreMultiply );

							buffer.WriteSBits( value, bits );
						}
						else
						{
							if ( Multiplies( field.PreMultiply ) )
								value = FtolWide( (float)(uint)value * field.PreMultiply );

							buffer.WriteBits( (uint)value, bits );
						}

						break;
					}

				case TypeAngle:
					buffer.WriteBitAngle( StructImage.ReadFloat( to, offset ), bits );
					break;

				case TypeTimeWindow8:
					buffer.WriteSBits( Ftol( time * 100.0 ) - Ftol( StructImage.ReadFloat( to, offset ) * 100f ), 8 );
					break;

				case TypeTimeWindowBig:
					buffer.WriteSBits( Ftol( field.PreMultiply * time ) - Ftol( field.PreMultiply * StructImage.ReadFloat( to, offset ) ), bits );
					break;

				case TypeString:
					for ( var i = offset; i < to.Length && to[i] != 0; i++ )
						buffer.WriteBits( to[i] >= 0x80 ? uint.MaxValue : to[i], 8 );

					buffer.WriteBits( 0, 8 );
					break;

				default:
					Log.Info( "unknown send field type\n" );
					break;
			}
		}
	}

	public int ParseDelta( MessageReader reader, byte[] from, byte[] to, double time )
	{
		var startBit = reader.CurrentBit;
		var bits = new byte[8];
		var byteCount = (int)reader.ReadBits( 3 );

		for ( var i = 0; i < byteCount; i++ )
			bits[i] = (byte)reader.ReadBits( 8 );

		for ( var index = 0; index < Fields.Length; index++ )
		{
			var field = Fields[index];
			var offset = field.Offset;
			var type = field.Type & int.MaxValue;
			var signed = field.Type < 0;

			if ( (bits[index >> 3] & (1 << (index & 7))) == 0 )
			{
				switch ( type )
				{
					case TypeByte:
						to[offset] = from[offset];
						break;

					case TypeShort:
						Array.Copy( from, offset, to, offset, 2 );
						break;

					case TypeFloat:
					case TypeInteger:
					case TypeAngle:
					case TypeTimeWindow8:
					case TypeTimeWindowBig:
						Array.Copy( from, offset, to, offset, 4 );
						break;

					case TypeString:
						Array.Copy( from, offset, to, offset, StringLength( from, offset ) + 1 );
						break;

					default:
						Log.Info( $"unparseable field type {type}\n" );
						break;
				}

				continue;
			}

			field.ReceivedCount++;

			switch ( type )
			{
				case TypeByte:
					if ( signed )
					{
						var value = (sbyte)reader.ReadSBits( field.SignificantBits );

						if ( Multiplies( field.PreMultiply ) )
							value = (sbyte)Ftol( value / field.PreMultiply );

						if ( Multiplies( field.PostMultiply ) )
							value = (sbyte)Ftol( value * field.PostMultiply );

						to[offset] = (byte)value;
					}
					else
					{
						var value = reader.ReadBits( field.SignificantBits );

						if ( Multiplies( field.PreMultiply ) )
							value = (uint)Ftol( (int)(value & 0xFF) / field.PreMultiply );

						var result = (byte)value;

						if ( Multiplies( field.PostMultiply ) )
							result = (byte)Ftol( (int)(value & 0xFF) * field.PostMultiply );

						to[offset] = result;
					}

					break;

				case TypeShort:
					if ( signed )
					{
						var value = (short)reader.ReadSBits( field.SignificantBits );

						if ( Multiplies( field.PreMultiply ) )
							value = (short)Ftol( value / field.PreMultiply );

						if ( Multiplies( field.PostMultiply ) )
							value = (short)Ftol( value * field.PostMultiply );

						StructImage.Write( to, offset, value );
					}
					else
					{
						var value = reader.ReadBits( field.SignificantBits ) & 0xFFFF;

						if ( Multiplies( field.PreMultiply ) )
							value = (uint)Ftol( value / field.PreMultiply ) & 0xFFFF;

						var result = (short)value;

						if ( Multiplies( field.PostMultiply ) )
							result = (short)Ftol( (int)value * field.PostMultiply );

						StructImage.Write( to, offset, result );
					}

					break;

				case TypeFloat:
					{
						var value = signed ? (float)reader.ReadSBits( field.SignificantBits ) : (float)reader.ReadBits( field.SignificantBits );

						if ( Multiplies( field.PreMultiply ) )
							value /= field.PreMultiply;

						if ( Multiplies( field.PostMultiply ) )
							value *= field.PostMultiply;

						StructImage.Write( to, offset, value );
						break;
					}

				case TypeInteger:
					if ( signed )
					{
						var value = reader.ReadSBits( field.SignificantBits );

						if ( Multiplies( field.PreMultiply ) )
							value = FtolWide( (float)(uint)value / field.PreMultiply );

						if ( Multiplies( field.PostMultiply ) )
							value = FtolWide( (float)(uint)value * field.PostMultiply );

						StructImage.Write( to, offset, value );
					}
					else
					{
						var value = (int)reader.ReadBits( field.SignificantBits );

						if ( Multiplies( field.PreMultiply ) )
							value = Ftol( value / field.PreMultiply );

						if ( Multiplies( field.PostMultiply ) )
							value = Ftol( value * field.PostMultiply );

						StructImage.Write( to, offset, value );
					}

					break;

				case TypeAngle:
					StructImage.Write( to, offset, reader.ReadBitAngle( field.SignificantBits ) );
					break;

				case TypeTimeWindow8:
					{
						var value = reader.ReadSBits( 8 );

						StructImage.Write( to, offset, ((float)Ftol( time * 100.0 ) - value) / 100f );
						break;
					}

				case TypeTimeWindowBig:
					{
						var value = reader.ReadSBits( field.SignificantBits );
						var result = (float)(field.PreMultiply * time) - value;

						if ( Multiplies( field.PreMultiply ) )
							result /= field.PreMultiply;

						StructImage.Write( to, offset, result );
						break;
					}

				case TypeString:
					for ( var i = offset; i < to.Length; i++ )
					{
						to[i] = (byte)reader.ReadBits( 8 );

						if ( to[i] == 0 )
							break;
					}

					break;

				default:
					Log.Info( $"unparseable field type {type}\n" );
					break;
			}
		}

		return reader.CurrentBit - startBit;
	}

	public int TestDelta( byte[] from, byte[] to )
	{
		var bits = 0;
		var highestBit = -1;

		for ( var index = 0; index < Fields.Length; index++ )
		{
			var field = Fields[index];
			var offset = field.Offset;
			bool different;

			switch ( field.Type & int.MaxValue )
			{
				case TypeByte:
					different = from[offset] != to[offset];
					break;

				case TypeShort:
					different = StructImage.ReadShort( from, offset ) != StructImage.ReadShort( to, offset );
					break;

				case TypeFloat:
				case TypeInteger:
				case TypeAngle:
					different = StructImage.ReadInt( from, offset ) != StructImage.ReadInt( to, offset );
					break;

				case TypeTimeWindow8:
					different = Ftol( StructImage.ReadFloat( from, offset ) * 100.0 ) != Ftol( StructImage.ReadFloat( to, offset ) * 100.0 );
					break;

				case TypeTimeWindowBig:
					different = Ftol( StructImage.ReadFloat( from, offset ) * 1000.0 ) != Ftol( StructImage.ReadFloat( to, offset ) * 1000.0 );
					break;

				case TypeString:
					{
						var length = StringLength( to, offset );
						var mismatch = false;

						for ( var i = 0; i < length && !mismatch; i++ )
							mismatch = char.ToLowerInvariant( (char)to[offset + i] ) != char.ToLowerInvariant( (char)from[offset + i] );

						if ( mismatch )
						{
							field.Flags |= FlagMark;
							bits += 8 + (length * 8);
						}

						continue;
					}

				default:
					Log.Info( $"Bad field type {field.Type & int.MaxValue}\n" );
					continue;
			}

			if ( !different )
				continue;

			bits += field.SignificantBits;
			highestBit = index;
		}

		return highestBit != -1 ? (highestBit & ~7) + 8 + bits : bits;
	}
}
