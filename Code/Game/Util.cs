using System;
using System.Globalization;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

public static class Util
{
	private const int MaxVectorString = 127;

	public static float Atof( string value )
	{
		var span = value.AsSpan().TrimStart();
		var length = 0;

		if ( length < span.Length && span[length] is '+' or '-' )
			length++;

		var digits = CountDigits( span, ref length );

		if ( length < span.Length && span[length] == '.' )
		{
			length++;
			digits += CountDigits( span, ref length );
		}

		if ( digits == 0 )
			return 0f;

		if ( length < span.Length && span[length] is 'e' or 'E' )
		{
			var exponent = length + 1;
			if ( exponent < span.Length && span[exponent] is '+' or '-' )
				exponent++;

			if ( CountDigits( span, ref exponent ) > 0 )
				length = exponent;
		}

		return (float)double.Parse( span[..length], NumberStyles.Float, CultureInfo.InvariantCulture );
	}

	public static int Atoi( string value )
	{
		var span = value.AsSpan().TrimStart();
		var position = 0;
		var negative = false;

		if ( position < span.Length && span[position] is '+' or '-' )
			negative = span[position++] == '-';

		long result = 0;

		for ( ; position < span.Length && char.IsAsciiDigit( span[position] ); position++ )
		{
			result = (result * 10) + (span[position] - '0');
			if ( result > (long)int.MaxValue + 1 )
				break;
		}

		return (int)Math.Clamp( negative ? -result : result, int.MinValue, int.MaxValue );
	}

	public static Vector3 StringToVector( string value )
	{
		if ( value.Length > MaxVectorString )
			value = value[..MaxVectorString];

		var vector = new Vector3();
		var front = 0;

		for ( var i = 0; i < 3; i++ )
		{
			vector[i] = Atof( value[front..] );

			var space = value.IndexOf( ' ', front );
			if ( space < 0 )
				break;

			front = space + 1;
		}

		return vector;
	}

	public static void StringToIntArray( int[] vector, string value )
	{
		if ( value.Length > MaxVectorString )
			value = value[..MaxVectorString];

		Array.Clear( vector );

		var front = 0;

		for ( var i = 0; i < vector.Length; i++ )
		{
			vector[i] = Atoi( value[front..] );

			var space = value.IndexOf( ' ', front );
			if ( space < 0 )
				break;

			front = space + 1;
		}
	}

	public static float AngleMod( float a )
	{
		if ( a < 0f )
			a += 360 * ((int)(a / 360) + 1);
		else if ( a >= 360f )
			a -= 360 * (int)(a / 360);

		return a;
	}

	public static float ApproachAngle( float target, float value, float speed )
	{
		target = AngleMod( target );
		value = AngleMod( target );

		var delta = target - value;

		if ( speed < 0f )
			speed = -speed;

		if ( delta < -180f )
			delta += 360f;
		else if ( delta > 180f )
			delta -= 360f;

		if ( delta > speed )
			value += speed;
		else if ( delta < -speed )
			value -= speed;
		else
			value = target;

		return value;
	}

	public static float AngleDistance( float next, float current )
	{
		var delta = next - current;

		if ( delta < -180f )
			delta += 360f;
		else if ( delta > 180f )
			delta -= 360f;

		return delta;
	}

	public static void MakeAimVectors( Vector3 angles, GlobalVars globals )
	{
		angles.x = -angles.x;
		globals.MakeVectors( angles );
	}

	public static void MakeInvVectors( Vector3 angles, GlobalVars globals )
	{
		globals.MakeVectors( angles );

		var forward = globals.Forward;
		var right = globals.Right * -1f;
		var up = globals.Up;

		globals.Forward = new Vector3( forward.x, right.x, up.x );
		globals.Right = new Vector3( forward.y, right.y, up.y );
		globals.Up = new Vector3( forward.z, right.z, up.z );
	}

	public static Vector3 Normalize( Vector3 v )
	{
		var length = MathLib.Length( v );
		if ( length == 0f )
			return new Vector3( 0f, 0f, 1f );

		length = 1f / length;
		return new Vector3( v.x * length, v.y * length, v.z * length );
	}

	private static readonly uint[] SeedTable =
	[
		28985, 27138, 26457, 9451, 17764, 10909, 28790, 8716, 6361, 4853, 17798, 21977, 19643, 20662, 10834, 20103,
		27067, 28634, 18623, 25849, 8576, 26234, 23887, 18228, 32587, 4836, 3306, 1811, 3035, 24559, 18399, 315,
		26766, 907, 24102, 12370, 9674, 2972, 10472, 16492, 22683, 11529, 27968, 30406, 13213, 2319, 23620, 16823,
		10013, 23772, 21567, 1251, 19579, 20313, 18241, 30130, 8402, 20807, 27354, 7169, 21211, 17293, 5410, 19223,
		10255, 22480, 27388, 9946, 15628, 24389, 17308, 2370, 9530, 31683, 25927, 23567, 11694, 26397, 32602, 15031,
		18255, 17582, 1422, 28835, 23607, 12597, 20602, 10138, 5212, 1252, 10074, 23166, 19823, 31667, 5902, 24630,
		18948, 14330, 14950, 8939, 23540, 21311, 22428, 22391, 3583, 29004, 30498, 18714, 4278, 2437, 22430, 3439,
		28313, 23161, 25396, 13471, 19324, 15287, 2563, 18901, 13103, 16867, 9714, 14322, 15197, 26889, 19372, 26241,
		31925, 14640, 11497, 8941, 10056, 6451, 28656, 10737, 13874, 17356, 8281, 25937, 1661, 4850, 7448, 12744,
		21826, 5477, 10167, 16705, 26897, 8839, 30947, 27978, 27283, 24685, 32298, 3525, 12398, 28726, 9475, 10208,
		617, 13467, 22287, 2376, 6097, 26312, 2974, 9114, 21787, 28010, 4725, 15387, 3274, 10762, 31695, 17320,
		18324, 12441, 16801, 27376, 22464, 7500, 5666, 18144, 15314, 31914, 31627, 6495, 5226, 31203, 2331, 4668,
		12650, 18275, 351, 7268, 31319, 30119, 7600, 2905, 13826, 11343, 13053, 15583, 30055, 31093, 5067, 761,
		9685, 11070, 21369, 27155, 3663, 26542, 20169, 12161, 15411, 30401, 7580, 31784, 8985, 29367, 20989, 14203,
		29694, 21167, 10337, 1706, 28578, 887, 3373, 19477, 14382, 675, 7033, 15111, 26138, 12252, 30996, 21409,
		25678, 18555, 13256, 23316, 22407, 16727, 991, 9236, 5373, 29402, 6117, 15241, 27715, 19291, 19888, 19847
	];

	private static uint _seed;

	private static Server Server => Server.Current;

	public static bool IsNullEnt( Edict edict ) => edict is null || edict.Index == 0;

	public static void Alert( AlertType type, string message ) => Server.AlertMessage( type, message );

	public static int SharedRandomLong( uint seed, int low, int high )
	{
		SeedRandom( (uint)((int)seed + low + high) );

		var range = (uint)(high - low + 1);

		if ( range - 1 == 0 )
			return low;

		var number = (int)NextRandom();

		return low + (int)(number % range);
	}

	public static float SharedRandomFloat( uint seed, float low, float high )
	{
		SeedRandom( (uint)((int)seed + BitConverter.SingleToInt32Bits( low ) + BitConverter.SingleToInt32Bits( high )) );

		NextRandom();
		NextRandom();

		var range = (uint)(high - low);

		if ( range == 0 )
			return low;

		var offset = (NextRandom() & 65535) / 65536f;

		return low + (offset * range);
	}

	private static uint NextRandom()
	{
		unchecked
		{
			_seed *= 69069;
			_seed += SeedTable[_seed & 0xff];

			return ++_seed & 0x0fffffff;
		}
	}

	private static void SeedRandom( uint seed )
	{
		_seed = SeedTable[seed & 0xff];
	}

	public static float AngleDiff( float destAngle, float srcAngle )
	{
		var delta = destAngle - srcAngle;

		if ( destAngle > srcAngle )
		{
			if ( delta >= 180f )
				delta -= 360f;
		}
		else if ( delta <= -180f )
		{
			delta += 360f;
		}

		return delta;
	}

	public static float Approach( float target, float value, float speed )
	{
		var delta = target - value;

		if ( delta > speed )
			value += speed;
		else if ( delta < -speed )
			value -= speed;
		else
			value = target;

		return value;
	}

	public static float SplineFraction( float value, float scale )
	{
		value = scale * value;

		var valueSquared = value * value;

		return (3f * valueSquared) - (2f * valueSquared * value);
	}

	public static float DotPoints( Vector3 source, Vector3 check, Vector3 direction )
	{
		var lineOfSight = Normalize2D( check - source );

		return (lineOfSight.x * direction.x) + (lineOfSight.y * direction.y);
	}

	public static Vector2 Normalize2D( Vector3 v )
	{
		var length = (float)Math.Sqrt( (v.x * v.x) + (v.y * v.y) );

		if ( length == 0f )
			return new Vector2( 0f, 0f );

		length = 1f / length;

		return new Vector2( v.x * length, v.y * length );
	}

	public static Vector3 ClampVectorToBox( Vector3 input, Vector3 clampSize )
	{
		for ( var i = 0; i < 3; i++ )
		{
			if ( input[i] > clampSize[i] )
				input[i] -= clampSize[i];
			else if ( input[i] < -clampSize[i] )
				input[i] += clampSize[i];
			else
				input[i] = 0f;
		}

		return Normalize( input );
	}

	public static BaseEntity FindEntityInSphere( BaseEntity start, Vector3 center, float radius )
	{
		var found = Server.FindEntityInSphere( start, center, radius );

		return IsNullEnt( found ) ? null : found as BaseEntity;
	}

	public static BaseEntity FindEntityByString( BaseEntity start, string keyword, string value )
	{
		var found = Server.FindEntityByString( start, keyword, value );

		return IsNullEnt( found ) ? null : found as BaseEntity;
	}

	public static BaseEntity FindEntityByClassname( BaseEntity start, string name ) => FindEntityByString( start, "classname", name );

	public static BaseEntity FindEntityByTargetname( BaseEntity start, string name ) => FindEntityByString( start, "targetname", name );

	public static BaseEntity FindEntityGeneric( string name, Vector3 source, float radius )
	{
		var entity = FindEntityByTargetname( null, name );

		if ( entity is not null )
			return entity;

		BaseEntity search = null;
		var maxDistance = radius * radius;

		while ( (search = FindEntityByClassname( search, name )) is not null )
		{
			var distance = MathLib.Length( search.Origin - source );

			distance *= distance;

			if ( maxDistance > distance )
			{
				entity = search;
				maxDistance = distance;
			}
		}

		return entity;
	}

	public static BaseEntity PlayerByIndex( int playerIndex )
	{
		if ( playerIndex > 0 && playerIndex <= Server.MaxClients )
			return Server.EntityOfEntIndex( playerIndex ) as BaseEntity;

		return null;
	}

	public static int EntitiesInBox( BaseEntity[] list, Vector3 mins, Vector3 maxs, EntityFlags flagMask )
	{
		var count = 0;

		if ( Server.EntityOfEntIndex( 1 ) is null )
			return count;

		for ( var i = 1; i < Server.NumEdicts; i++ )
		{
			if ( Server.GetEdict( i ) is not BaseEntity entity )
				continue;

			if ( flagMask != 0 && (entity.EntityFlags & flagMask) == 0 )
				continue;

			if ( mins.x > entity.AbsMax.x || mins.y > entity.AbsMax.y || mins.z > entity.AbsMax.z
				|| maxs.x < entity.AbsMin.x || maxs.y < entity.AbsMin.y || maxs.z < entity.AbsMin.z )
			{
				continue;
			}

			list[count++] = entity;

			if ( count >= list.Length )
				return count;
		}

		return count;
	}

	public static int MonstersInSphere( BaseEntity[] list, Vector3 center, float radius )
	{
		var count = 0;
		var radiusSquared = radius * radius;

		if ( Server.EntityOfEntIndex( 1 ) is null )
			return count;

		for ( var i = 1; i < Server.NumEdicts; i++ )
		{
			if ( Server.GetEdict( i ) is not BaseEntity entity )
				continue;

			if ( (entity.EntityFlags & (EntityFlags.Client | EntityFlags.Monster)) == 0 )
				continue;

			var delta = center.x - entity.Origin.x;

			delta *= delta;

			if ( delta > radiusSquared )
				continue;

			var distance = delta;

			delta = center.y - entity.Origin.y;
			delta *= delta;
			distance += delta;

			if ( distance > radiusSquared )
				continue;

			delta = center.z - ((entity.AbsMin.z + entity.AbsMax.z) * 0.5f);
			delta *= delta;
			distance += delta;

			if ( distance > radiusSquared )
				continue;

			list[count++] = entity;

			if ( count >= list.Length )
				return count;
		}

		return count;
	}

	public static Trace TraceLine( Vector3 start, Vector3 end, int ignoreMonsters, Edict ignore )
	{
		return Server.TraceLine( start, end, ignoreMonsters, ignore );
	}

	public static Trace TraceHull( Vector3 start, Vector3 end, int ignoreMonsters, int hullNumber, Edict ignore )
	{
		return Server.TraceHull( start, end, ignoreMonsters, hullNumber, ignore );
	}

	public static Trace TraceModel( Vector3 start, Vector3 end, int hullNumber, Edict model )
	{
		return Server.TraceModel( start, end, hullNumber, model );
	}

	public static int PointContents( Vector3 point ) => Server.PointContents( point );

	public static bool IsValidEntity( Edict edict )
	{
		return edict is not null && edict.IsValid && (edict.EntityFlags & EntityFlags.KillMe) != EntityFlags.KillMe;
	}

	public static void PrecacheOther( string className )
	{
		if ( Server.CreateNamedEntity( className ) is not BaseEntity entity )
		{
			Util.Alert( AlertType.Console, "NULL Ent in UTIL_PrecacheOther" );
			return;
		}

		entity.Precache();

		Server.FreeEdict( entity );
	}

	private static ushort FixedUnsigned16( float value, float scale )
	{
		return (ushort)Math.Clamp( (int)(value * scale), 0, 0xFFFF );
	}

	private static short FixedSigned16( float value, float scale )
	{
		return (short)Math.Clamp( (int)(value * scale), -32768, 32767 );
	}

	public static void ScreenShake( Vector3 center, float amplitude, float frequency, float duration, float radius )
	{
		var shakeDuration = FixedUnsigned16( duration, 1 << 12 );
		var shakeFrequency = FixedUnsigned16( frequency, 1 << 8 );

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			var player = PlayerByIndex( i );

			if ( player is null || (player.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
				continue;

			var localAmplitude = 0f;

			if ( radius <= 0f )
				localAmplitude = amplitude;
			else if ( MathLib.Length( center - player.Origin ) < radius )
				localAmplitude = amplitude;

			if ( localAmplitude == 0f )
				continue;

			Server.MessageBegin( MessageDest.One, UserMessages.Shake, default, player );
			Server.WriteShort( FixedUnsigned16( localAmplitude, 1 << 12 ) );
			Server.WriteShort( shakeDuration );
			Server.WriteShort( shakeFrequency );
			Server.MessageEnd();
		}
	}

	public static void ScreenShakeAll( Vector3 center, float amplitude, float frequency, float duration )
	{
		ScreenShake( center, amplitude, frequency, duration, 0f );
	}

	public const int FadeIn = 0x0000;
	public const int FadeOut = 0x0001;
	public const int FadeModulate = 0x0002;
	public const int FadeStayOut = 0x0004;

	public static void ScreenFadeAll( Vector3 color, float fadeTime, float fadeHold, int alpha, int flags )
	{
		for ( var i = 1; i <= Server.MaxClients; i++ )
			ScreenFade( PlayerByIndex( i ), color, fadeTime, fadeHold, alpha, flags );
	}

	public static void ScreenFade( BaseEntity entity, Vector3 color, float fadeTime, float fadeHold, int alpha, int flags )
	{
		if ( entity is null || !entity.IsNetClient )
			return;

		Server.MessageBegin( MessageDest.One, UserMessages.Fade, default, entity );
		Server.WriteShort( FixedUnsigned16( fadeTime, 1 << 12 ) );
		Server.WriteShort( FixedUnsigned16( fadeHold, 1 << 12 ) );
		Server.WriteShort( flags );
		Server.WriteByte( (int)color.x );
		Server.WriteByte( (int)color.y );
		Server.WriteByte( (int)color.z );
		Server.WriteByte( alpha );
		Server.MessageEnd();
	}

	public static void HudMessage( BaseEntity entity, in HudTextParms parms, string message )
	{
		if ( entity is null || !entity.IsNetClient )
			return;

		Server.MessageBegin( MessageDest.One, Server.TempEntityMessage, default, entity );
		Server.WriteByte( TempEntity.TextMessage );
		Server.WriteByte( parms.Channel & 0xFF );
		Server.WriteShort( FixedSigned16( parms.X, 1 << 13 ) );
		Server.WriteShort( FixedSigned16( parms.Y, 1 << 13 ) );
		Server.WriteByte( parms.Effect );
		Server.WriteByte( parms.R1 );
		Server.WriteByte( parms.G1 );
		Server.WriteByte( parms.B1 );
		Server.WriteByte( parms.A1 );
		Server.WriteByte( parms.R2 );
		Server.WriteByte( parms.G2 );
		Server.WriteByte( parms.B2 );
		Server.WriteByte( parms.A2 );
		Server.WriteShort( FixedUnsigned16( parms.FadeInTime, 1 << 8 ) );
		Server.WriteShort( FixedUnsigned16( parms.FadeOutTime, 1 << 8 ) );
		Server.WriteShort( FixedUnsigned16( parms.HoldTime, 1 << 8 ) );

		if ( parms.Effect == 2 )
			Server.WriteShort( FixedUnsigned16( parms.FxTime, 1 << 8 ) );

		Server.WriteString( message.Length < 512 ? message : message[..511] );
		Server.MessageEnd();
	}

	public static void HudMessageAll( in HudTextParms parms, string message )
	{
		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( PlayerByIndex( i ) is { } player )
				HudMessage( player, parms, message );
		}
	}

	public static void ClientPrintAll( int destination, string name, string param1 = null, string param2 = null, string param3 = null, string param4 = null )
	{
		Server.MessageBegin( MessageDest.All, UserMessages.TextMsg );
		WriteTextMsg( destination, name, param1, param2, param3, param4 );
	}

	public static void ClientPrint( Edict client, int destination, string name, string param1 = null, string param2 = null, string param3 = null, string param4 = null )
	{
		Server.MessageBegin( MessageDest.One, UserMessages.TextMsg, default, client );
		WriteTextMsg( destination, name, param1, param2, param3, param4 );
	}

	private static void WriteTextMsg( int destination, string name, string param1, string param2, string param3, string param4 )
	{
		Server.WriteByte( destination );
		Server.WriteString( name );

		if ( param1 is not null )
			Server.WriteString( param1 );

		if ( param2 is not null )
			Server.WriteString( param2 );

		if ( param3 is not null )
			Server.WriteString( param3 );

		if ( param4 is not null )
			Server.WriteString( param4 );

		Server.MessageEnd();
	}

	public static void SayText( string text, BaseEntity entity )
	{
		if ( !entity.IsNetClient )
			return;

		Server.MessageBegin( MessageDest.One, UserMessages.SayText, default, entity );
		Server.WriteByte( entity.Index );
		Server.WriteString( text );
		Server.MessageEnd();
	}

	public static void SayTextAll( string text, BaseEntity entity )
	{
		Server.MessageBegin( MessageDest.All, UserMessages.SayText );
		Server.WriteByte( entity.Index );
		Server.WriteString( text );
		Server.MessageEnd();
	}

	public static void ShowMessage( string message, BaseEntity entity )
	{
		if ( entity is null || !entity.IsNetClient )
			return;

		Server.MessageBegin( MessageDest.One, UserMessages.HudText, default, entity );
		Server.WriteString( message );
		Server.MessageEnd();
	}

	public static void ShowMessageAll( string message )
	{
		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( PlayerByIndex( i ) is { } player )
				ShowMessage( message, player );
		}
	}

	public static bool ShouldShowBlood( int color )
	{
		if ( color == BaseEntity.DontBleed )
			return false;

		return Cvar.GetFloat( color == BaseEntity.BloodColorRed ? "violence_hblood" : "violence_ablood" ) != 0f;
	}

	public static void BloodStream( Vector3 origin, Vector3 direction, int color, int amount )
	{
		if ( !ShouldShowBlood( color ) )
			return;

		if ( HalfLifeDll.Language == HalfLifeDll.LanguageGerman && color == BaseEntity.BloodColorRed )
			color = 0;

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, origin );
		Server.WriteByte( TempEntity.BloodStream );
		Server.WriteCoord( origin.x );
		Server.WriteCoord( origin.y );
		Server.WriteCoord( origin.z );
		Server.WriteCoord( direction.x );
		Server.WriteCoord( direction.y );
		Server.WriteCoord( direction.z );
		Server.WriteByte( color );
		Server.WriteByte( Math.Min( amount, 255 ) );
		Server.MessageEnd();
	}

	public static void BloodDrips( Vector3 origin, Vector3 direction, int color, int amount )
	{
		if ( !ShouldShowBlood( color ) )
			return;

		if ( color == BaseEntity.DontBleed || amount == 0 )
			return;

		if ( HalfLifeDll.Language == HalfLifeDll.LanguageGerman && color == BaseEntity.BloodColorRed )
			color = 0;

		if ( HalfLifeDll.GameRules.IsMultiplayer() )
			amount *= 2;

		if ( amount > 255 )
			amount = 255;

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, origin );
		Server.WriteByte( TempEntity.BloodSprite );
		Server.WriteCoord( origin.x );
		Server.WriteCoord( origin.y );
		Server.WriteCoord( origin.z );
		Server.WriteShort( GameModels.BloodSpray );
		Server.WriteShort( GameModels.BloodDrop );
		Server.WriteByte( color );
		Server.WriteByte( Math.Min( Math.Max( 3, amount / 10 ), 16 ) );
		Server.MessageEnd();
	}

	public static Vector3 RandomBloodVector()
	{
		var x = Server.RandomFloat( -1f, 1f );
		var y = Server.RandomFloat( -1f, 1f );
		var z = Server.RandomFloat( 0f, 1f );

		return new Vector3( x, y, z );
	}

	public static void BloodDecalTrace( in Trace trace, int bloodColor )
	{
		if ( !ShouldShowBlood( bloodColor ) )
			return;

		if ( bloodColor == BaseEntity.BloodColorRed )
			DecalTrace( trace, Decals.Blood1 + Server.RandomLong( 0, 5 ) );
		else
			DecalTrace( trace, Decals.YellowBlood1 + Server.RandomLong( 0, 5 ) );
	}

	public static void DecalTrace( in Trace trace, int decalNumber )
	{
		if ( decalNumber < 0 )
			return;

		var index = Decals.Index[decalNumber];

		if ( index < 0 )
			return;

		if ( trace.Fraction == 1f )
			return;

		var entityIndex = 0;

		if ( trace.Ent is not null )
		{
			if ( trace.Ent is BaseEntity entity && !entity.IsBspModel )
				return;

			entityIndex = trace.Ent.Index;
		}

		var message = TempEntity.Decal;

		if ( entityIndex != 0 )
		{
			if ( index > 255 )
			{
				message = TempEntity.DecalHigh;
				index -= 256;
			}
		}
		else
		{
			message = TempEntity.WorldDecal;

			if ( index > 255 )
			{
				message = TempEntity.WorldDecalHigh;
				index -= 256;
			}
		}

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( message );
		Server.WriteCoord( trace.EndPos.x );
		Server.WriteCoord( trace.EndPos.y );
		Server.WriteCoord( trace.EndPos.z );
		Server.WriteByte( index );

		if ( entityIndex != 0 )
			Server.WriteShort( entityIndex );

		Server.MessageEnd();
	}

	public static void PlayerDecalTrace( in Trace trace, int playerNumber, int decalNumber, bool custom )
	{
		int index;

		if ( !custom )
		{
			if ( decalNumber < 0 )
				return;

			index = Decals.Index[decalNumber];

			if ( index < 0 )
				return;
		}
		else
		{
			index = decalNumber;
		}

		if ( trace.Fraction == 1f )
			return;

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.PlayerDecal );
		Server.WriteByte( playerNumber );
		Server.WriteCoord( trace.EndPos.x );
		Server.WriteCoord( trace.EndPos.y );
		Server.WriteCoord( trace.EndPos.z );
		Server.WriteShort( (short)(trace.Ent?.Index ?? 0) );
		Server.WriteByte( index );
		Server.MessageEnd();
	}

	public static void GunshotDecalTrace( in Trace trace, int decalNumber )
	{
		if ( decalNumber < 0 )
			return;

		var index = Decals.Index[decalNumber];

		if ( index < 0 )
			return;

		if ( trace.Fraction == 1f )
			return;

		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, trace.EndPos );
		Server.WriteByte( TempEntity.GunshotDecal );
		Server.WriteCoord( trace.EndPos.x );
		Server.WriteCoord( trace.EndPos.y );
		Server.WriteCoord( trace.EndPos.z );
		Server.WriteShort( (short)(trace.Ent?.Index ?? 0) );
		Server.WriteByte( index );
		Server.MessageEnd();
	}

	public static void ParticleEffect( Vector3 origin, Vector3 direction, uint color, uint count )
	{
		Server.ParticleEffect( origin, direction, color, count );
	}

	public static void Sparks( Vector3 position )
	{
		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, position );
		Server.WriteByte( TempEntity.Sparks );
		Server.WriteCoord( position.x );
		Server.WriteCoord( position.y );
		Server.WriteCoord( position.z );
		Server.MessageEnd();
	}

	public static void StreakSplash( Vector3 origin, Vector3 direction, int color, int count, int speed, int velocityRange )
	{
		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, origin );
		Server.WriteByte( TempEntity.StreakSplash );
		Server.WriteCoord( origin.x );
		Server.WriteCoord( origin.y );
		Server.WriteCoord( origin.z );
		Server.WriteCoord( direction.x );
		Server.WriteCoord( direction.y );
		Server.WriteCoord( direction.z );
		Server.WriteByte( color );
		Server.WriteShort( count );
		Server.WriteShort( speed );
		Server.WriteShort( velocityRange );
		Server.MessageEnd();
	}

	public static void Ricochet( Vector3 position, float scale )
	{
		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, position );
		Server.WriteByte( TempEntity.ArmorRicochet );
		Server.WriteCoord( position.x );
		Server.WriteCoord( position.y );
		Server.WriteCoord( position.z );
		Server.WriteByte( (int)(scale * 10f) );
		Server.MessageEnd();
	}

	public static bool TeamsMatch( string teamName1, string teamName2 )
	{
		if ( !HalfLifeDll.GameRules.IsTeamplay() )
			return true;

		return teamName1.Length != 0 && teamName2.Length != 0 && teamName1.Equals( teamName2, StringComparison.OrdinalIgnoreCase );
	}

	public static float WaterLevel( Vector3 position, float minZ, float maxZ )
	{
		var midUp = position;

		midUp.z = minZ;

		if ( PointContents( midUp ) != (int)Contents.Water )
			return minZ;

		midUp.z = maxZ;

		if ( PointContents( midUp ) == (int)Contents.Water )
			return maxZ;

		var difference = maxZ - minZ;

		while ( difference > 1.0 )
		{
			midUp.z = (float)(minZ + (difference / 2.0));

			if ( PointContents( midUp ) == (int)Contents.Water )
				minZ = midUp.z;
			else
				maxZ = midUp.z;

			difference = maxZ - minZ;
		}

		return midUp.z;
	}

	public static void Bubbles( Vector3 mins, Vector3 maxs, int count )
	{
		var mid = (mins + maxs) * 0.5f;
		var height = WaterLevel( mid, mid.z, mid.z + 1024f ) - mins.z;

		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, mid );
		Server.WriteByte( TempEntity.Bubbles );
		Server.WriteCoord( mins.x );
		Server.WriteCoord( mins.y );
		Server.WriteCoord( mins.z );
		Server.WriteCoord( maxs.x );
		Server.WriteCoord( maxs.y );
		Server.WriteCoord( maxs.z );
		Server.WriteCoord( height );
		Server.WriteShort( GameModels.Bubbles );
		Server.WriteByte( count );
		Server.WriteCoord( 8f );
		Server.MessageEnd();
	}

	public static void BubbleTrail( Vector3 from, Vector3 to, int count )
	{
		var height = WaterLevel( from, from.z, from.z + 256f ) - from.z;

		if ( height < 8f )
		{
			height = WaterLevel( to, to.z, to.z + 256f ) - to.z;

			if ( height < 8f )
				return;

			height = height + to.z - from.z;
		}

		if ( count > 255 )
			count = 255;

		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BubbleTrail );
		Server.WriteCoord( from.x );
		Server.WriteCoord( from.y );
		Server.WriteCoord( from.z );
		Server.WriteCoord( to.x );
		Server.WriteCoord( to.y );
		Server.WriteCoord( to.z );
		Server.WriteCoord( height );
		Server.WriteShort( GameModels.Bubbles );
		Server.WriteByte( count );
		Server.WriteCoord( 8f );
		Server.MessageEnd();
	}

	private static int CountDigits( ReadOnlySpan<char> span, ref int position )
	{
		var start = position;

		while ( position < span.Length && char.IsAsciiDigit( span[position] ) )
			position++;

		return position - start;
	}
}
