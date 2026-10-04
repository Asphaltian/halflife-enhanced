using System;
using GoldSrc;

namespace HalfLife;

public static class Sentences
{
	private const int MaxSentences = 2048;
	private const int MaxSentenceName = 16;
	private const int MaxGroups = 200;
	private const int MaxLru = 32;
	private const int MaxLineLength = 511;
	private const byte Used = 0xFF;

	private sealed class Group
	{
		public string Name;
		public int Count;
		public byte[] Lru = new byte[MaxLru];
	}

	private static List<Group> _groups = [];
	private static List<string> _allNames = [];

	private static bool _initialized;

	public static void Init()
	{
		if ( _initialized )
			return;

		_allNames.Clear();
		_groups.Clear();

		if ( Server.LoadMountText( "sound/sentences.txt" ) is not { } file )
			return;

		var position = 0;
		string currentGroup = null;

		while ( Common.MemFgets( file, ref position, MaxLineLength ) is { } line )
		{
			var i = 0;

			while ( i < line.Length && line[i] == ' ' )
				i++;

			if ( i >= line.Length )
				continue;

			if ( line[i] == '/' || !char.IsAsciiLetter( line[i] ) )
				continue;

			var j = i;

			while ( j < line.Length && line[j] != ' ' )
				j++;

			if ( j >= line.Length )
				continue;

			if ( _allNames.Count > MaxSentences )
			{
				Log.Error( "Too many sentences in sentences.txt!" );
				break;
			}

			var name = line[i..j];

			if ( name.Length >= MaxSentenceName )
				Log.Warning( $"Sentence {name} longer than {MaxSentenceName - 1} letters" );

			_allNames.Add( name );

			j--;

			if ( j <= i )
				continue;

			if ( !char.IsAsciiDigit( line[j] ) )
				continue;

			while ( j > i && char.IsAsciiDigit( line[j] ) )
				j--;

			if ( j <= i )
				continue;

			var group = line[i..(j + 1)];

			if ( group != currentGroup )
			{
				if ( _groups.Count >= MaxGroups )
				{
					Log.Error( "Too many sentence groups in sentences.txt!" );
					break;
				}

				_groups.Add( new Group { Name = group, Count = 1 } );
				currentGroup = group;
			}
			else
			{
				_groups[^1].Count++;
			}
		}

		_initialized = true;

		foreach ( var group in _groups )
			InitLru( group.Lru, group.Count );
	}

	public static int GetIndex( string groupName )
	{
		if ( !_initialized || groupName is null )
			return -1;

		return _groups.FindIndex( x => x.Name == groupName );
	}

	public static int PlayRandom( Edict entity, int group, float volume, float attenuation, int flags, int pitch )
	{
		if ( !_initialized )
			return -1;

		var pick = Pick( group, out var name );

		if ( pick > 0 )
			Sound.EmitDyn( entity, SoundChannel.Voice, name, volume, attenuation, flags, pitch );

		return pick;
	}

	public static int PlayRandom( Edict entity, string groupName, float volume, float attenuation, int flags, int pitch )
	{
		if ( !_initialized )
			return -1;

		var group = GetIndex( groupName );

		if ( group < 0 )
		{
			Log.Info( $"No such sentence group {groupName}" );
			return -1;
		}

		var pick = Pick( group, out var name );

		if ( pick >= 0 && !string.IsNullOrEmpty( name ) )
			Sound.EmitDyn( entity, SoundChannel.Voice, name, volume, attenuation, flags, pitch );

		return pick;
	}

	public static int PlaySequential( Edict entity, string groupName, float volume, float attenuation, int flags, int pitch, int pick, bool reset )
	{
		if ( !_initialized )
			return -1;

		var group = GetIndex( groupName );

		if ( group < 0 )
			return -1;

		var next = PickSequential( group, out var name, pick, reset );

		if ( next >= 0 && !string.IsNullOrEmpty( name ) )
			Sound.EmitDyn( entity, SoundChannel.Voice, name, volume, attenuation, flags, pitch );

		return next;
	}

	public static void Stop( Edict entity, int group, int pick )
	{
		if ( !_initialized )
			return;

		if ( group < 0 || pick < 0 )
			return;

		Sound.Stop( entity, SoundChannel.Voice, $"!{_groups[group].Name}{pick}" );
	}

	public static string Name( int index ) => _allNames[index];

	public static int Lookup( string sample, out string sentence )
	{
		var index = _allNames.FindIndex( x => x.Equals( sample[1..], StringComparison.OrdinalIgnoreCase ) );

		sentence = index >= 0 ? $"!{index}" : null;

		return index;
	}

	private static void InitLru( byte[] lru, int count )
	{
		if ( !_initialized )
			return;

		if ( count > MaxLru )
			count = MaxLru;

		for ( var i = 0; i < count; i++ )
			lru[i] = (byte)i;

		for ( var i = 0; i < count * 4; i++ )
		{
			var j = Server.RandomLong( 0, count - 1 );
			var k = Server.RandomLong( 0, count - 1 );

			(lru[j], lru[k]) = (lru[k], lru[j]);
		}
	}

	private static int Pick( int group, out string found )
	{
		found = null;

		if ( !_initialized )
			return -1;

		if ( group < 0 )
			return -1;

		var lru = _groups[group].Lru;
		var count = Math.Min( (int)(byte)_groups[group].Count, MaxLru );

		while ( true )
		{
			for ( var i = 0; i < count; i++ )
			{
				if ( lru[i] == Used )
					continue;

				var pick = lru[i];

				lru[i] = Used;
				found = $"!{_groups[group].Name}{pick}";

				return pick;
			}

			InitLru( lru, count );
		}
	}

	private static int PickSequential( int group, out string found, int pick, bool reset )
	{
		found = null;

		if ( !_initialized )
			return -1;

		if ( group < 0 )
			return -1;

		var count = (byte)_groups[group].Count;

		if ( count == 0 )
			return -1;

		if ( pick >= count )
			pick = count - 1;

		found = $"!{_groups[group].Name}{pick}";

		if ( pick >= count )
			return reset ? 0 : count;

		return pick + 1;
	}
}

public static class Sound
{
	public static void Emit( Edict entity, SoundChannel channel, string sample, float volume, float attenuation )
	{
		EmitDyn( entity, channel, sample, volume, attenuation, 0, Server.PitchNormal );
	}

	public static void EmitDyn( Edict entity, SoundChannel channel, string sample, float volume, float attenuation, int flags, int pitch )
	{
		if ( entity.ClientSide )
			return;

		if ( sample is not null && sample.StartsWith( '!' ) )
		{
			if ( Sentences.Lookup( sample, out var name ) >= 0 )
				entity.Server.EmitSound( entity, channel, name, volume, attenuation, flags, pitch );
			else
				Log.Info( $"Unable to find {sample} in sentences.txt" );

			return;
		}

		entity.Server.EmitSound( entity, channel, sample, volume, attenuation, flags, pitch );
	}

	public static void Stop( Edict entity, SoundChannel channel, string sample )
	{
		EmitDyn( entity, channel, sample, 0f, 0f, Server.SoundStop, Server.PitchNormal );
	}

	public static void EmitAmbient( Edict entity, Vector3 origin, string sample, float volume, float attenuation, int flags, int pitch )
	{
		if ( sample is not null && sample.StartsWith( '!' ) )
		{
			if ( Sentences.Lookup( sample, out var name ) >= 0 )
				entity.Server.EmitAmbientSound( entity, origin, name, volume, attenuation, flags, pitch );

			return;
		}

		entity.Server.EmitAmbientSound( entity, origin, sample, volume, attenuation, flags, pitch );
	}

	public static void EmitSuit( Edict entity, string sample )
	{
		var volume = Cvar.GetFloat( "suitvolume" );
		var pitch = Server.PitchNormal;

		if ( Server.RandomLong( 0, 1 ) != 0 )
			pitch = Server.RandomLong( 0, 6 ) + 98;

		if ( volume > 0.05 )
			EmitDyn( entity, SoundChannel.Static, sample, volume, Server.AttenuationNormal, 0, pitch );
	}

	public static void EmitGroupSuit( Edict entity, int group )
	{
		var volume = Cvar.GetFloat( "suitvolume" );
		var pitch = Server.PitchNormal;

		if ( Server.RandomLong( 0, 1 ) != 0 )
			pitch = Server.RandomLong( 0, 6 ) + 98;

		if ( volume > 0.05 )
			Sentences.PlayRandom( entity, group, volume, Server.AttenuationNormal, 0, pitch );
	}

	public static void EmitGroupSuit( Edict entity, string groupName )
	{
		var volume = Cvar.GetFloat( "suitvolume" );
		var pitch = Server.PitchNormal;

		if ( Server.RandomLong( 0, 1 ) != 0 )
			pitch = Server.RandomLong( 0, 6 ) + 98;

		if ( volume > 0.05 )
			Sentences.PlayRandom( entity, groupName, volume, Server.AttenuationNormal, 0, pitch );
	}
}

public static class TextureType
{
	public const char Concrete = 'C';
	public const char Metal = 'M';
	public const char Dirt = 'D';
	public const char Vent = 'V';
	public const char Grate = 'G';
	public const char Tile = 'T';
	public const char Slosh = 'S';
	public const char Wood = 'W';
	public const char Computer = 'P';
	public const char Glass = 'Y';
	public const char Flesh = 'F';

	private const int MaxTextures = 512;
	private const int MaxTextureName = 13;
	private const int MaxLineLength = 511;

	private static List<(string Name, char Type)> _textures = [];

	private static bool _initialized;

	public static void Init()
	{
		if ( _initialized )
			return;

		_textures.Clear();

		if ( Server.LoadMountText( "sound/materials.txt" ) is not { } file )
			return;

		var position = 0;

		while ( Common.MemFgets( file, ref position, MaxLineLength ) is { } line && _textures.Count < MaxTextures )
		{
			var i = 0;

			while ( i < line.Length && char.IsWhiteSpace( line[i] ) )
				i++;

			if ( i >= line.Length )
				continue;

			if ( line[i] == '/' || !char.IsAsciiLetter( line[i] ) )
				continue;

			var type = char.ToUpperInvariant( line[i++] );

			while ( i < line.Length && char.IsWhiteSpace( line[i] ) )
				i++;

			if ( i >= line.Length )
				continue;

			var j = i;

			while ( j < line.Length && !char.IsWhiteSpace( line[j] ) )
				j++;

			if ( j >= line.Length )
				continue;

			j = Math.Min( j, MaxTextureName - 1 + i );

			_textures.Add( (line[i..j], type) );
		}

		_initialized = true;
	}

	public static char Find( string name )
	{
		foreach ( var texture in _textures )
		{
			if ( string.Compare( name, 0, texture.Name, 0, MaxTextureName - 1, StringComparison.OrdinalIgnoreCase ) == 0 )
				return texture.Type;
		}

		return Concrete;
	}

	public static float PlaySound( in Trace trace, Vector3 start, Vector3 end, Bullet bulletType )
	{
		if ( !HalfLifeDll.GameRules.PlayTextureSounds() )
			return 0f;

		var server = Server.Current;
		var world = server.GetEdict( 0 );
		var entity = (trace.Ent ?? world) as BaseEntity;
		var textureType = '\0';

		if ( entity is not null && entity.Classify() != BaseEntity.ClassNone && entity.Classify() != BaseEntity.ClassMachine )
		{
			textureType = Flesh;
		}
		else
		{
			var textureName = server.TraceTexture( entity ?? world, start, end );

			if ( textureName is not null )
			{
				var skip = 0;

				if ( textureName.StartsWith( '-' ) || textureName.StartsWith( '+' ) )
					skip += 2;

				if ( skip < textureName.Length && textureName[skip] is '{' or '!' or '~' or ' ' )
					skip++;

				textureName = skip < textureName.Length ? textureName[skip..] : string.Empty;

				if ( textureName.Length > MaxTextureName - 1 )
					textureName = textureName[..(MaxTextureName - 1)];

				textureType = Find( textureName );
			}
		}

		float volume;
		float volumeBar;
		string[] samples;
		var attenuation = Server.AttenuationNormal;

		switch ( textureType )
		{
			default:
				volume = 0.9f;
				volumeBar = 0.6f;
				samples = ["player/pl_step1.wav", "player/pl_step2.wav"];
				break;
			case Metal:
				volume = 0.9f;
				volumeBar = 0.3f;
				samples = ["player/pl_metal1.wav", "player/pl_metal2.wav"];
				break;
			case Dirt:
				volume = 0.9f;
				volumeBar = 0.1f;
				samples = ["player/pl_dirt1.wav", "player/pl_dirt2.wav", "player/pl_dirt3.wav"];
				break;
			case Vent:
				volume = 0.5f;
				volumeBar = 0.3f;
				samples = ["player/pl_duct1.wav", "player/pl_duct1.wav"];
				break;
			case Grate:
				volume = 0.9f;
				volumeBar = 0.5f;
				samples = ["player/pl_grate1.wav", "player/pl_grate4.wav"];
				break;
			case Tile:
				volume = 0.8f;
				volumeBar = 0.2f;
				samples = ["player/pl_tile1.wav", "player/pl_tile3.wav", "player/pl_tile2.wav", "player/pl_tile4.wav"];
				break;
			case Slosh:
				volume = 0.9f;
				volumeBar = 0f;
				samples = ["player/pl_slosh1.wav", "player/pl_slosh3.wav", "player/pl_slosh2.wav", "player/pl_slosh4.wav"];
				break;
			case Wood:
				volume = 0.9f;
				volumeBar = 0.2f;
				samples = ["debris/wood1.wav", "debris/wood2.wav", "debris/wood3.wav"];
				break;
			case Glass:
			case Computer:
				volume = 0.8f;
				volumeBar = 0.2f;
				samples = ["debris/glass1.wav", "debris/glass2.wav", "debris/glass3.wav"];
				break;
			case Flesh:
				if ( bulletType == Bullet.PlayerCrowbar )
					return 0f;

				volume = 1f;
				volumeBar = 0.2f;
				samples = ["weapons/bullet_hit1.wav", "weapons/bullet_hit2.wav"];
				attenuation = 1f;
				break;
		}

		if ( entity is not null && entity.ClassName == "func_breakable" )
		{
			volume /= 1.5f;
			volumeBar /= 2f;
		}
		else if ( textureType == Computer )
		{
			if ( trace.Fraction != 1f && Server.RandomLong( 0, 1 ) != 0 )
			{
				Util.Sparks( trace.EndPos );

				var sparkVolume = Server.RandomFloat( 0.7f, 1f );

				switch ( Server.RandomLong( 0, 1 ) )
				{
					case 0:
						Sound.EmitAmbient( world, trace.EndPos, "buttons/spark5.wav", sparkVolume, Server.AttenuationNormal, 0, 100 );
						break;
					case 1:
						Sound.EmitAmbient( world, trace.EndPos, "buttons/spark6.wav", sparkVolume, Server.AttenuationNormal, 0, 100 );
						break;
				}
			}
		}

		Sound.EmitAmbient( world, trace.EndPos, samples[Server.RandomLong( 0, samples.Length - 1 )], volume, attenuation, 0, 96 + Server.RandomLong( 0, 0xf ) );

		return volumeBar;
	}
}

public sealed record DynPitchVol
{
	[Save( FieldType.Integer )]
	public int Preset;
	[Save( FieldType.Integer )]
	public int PitchRun;
	[Save( FieldType.Integer )]
	public int PitchStart;
	[Save( FieldType.Integer )]
	public int SpinUp;
	[Save( FieldType.Integer )]
	public int SpinDown;
	[Save( FieldType.Integer )]
	public int VolumeRun;
	[Save( FieldType.Integer )]
	public int VolumeStart;
	[Save( FieldType.Integer )]
	public int FadeIn;
	[Save( FieldType.Integer )]
	public int FadeOut;
	[Save( FieldType.Integer )]
	public int LfoType;
	[Save( FieldType.Integer )]
	public int LfoRate;
	[Save( FieldType.Integer )]
	public int LfoModPitch;
	[Save( FieldType.Integer )]
	public int LfoModVolume;
	[Save( FieldType.Integer )]
	public int CountSpinUp;
	[Save( FieldType.Integer )]
	public int SpinCount;
	[Save( FieldType.Integer )]
	public int Pitch;
	[Save( FieldType.Integer )]
	public int SpinUpSaved;
	[Save( FieldType.Integer )]
	public int SpinDownSaved;
	[Save( FieldType.Integer )]
	public int PitchFraction;
	[Save( FieldType.Integer )]
	public int Volume;
	[Save( FieldType.Integer )]
	public int FadeInSaved;
	[Save( FieldType.Integer )]
	public int FadeOutSaved;
	[Save( FieldType.Integer )]
	public int VolumeFraction;
	[Save( FieldType.Integer )]
	public int LfoFraction;
	[Save( FieldType.Integer )]
	public int LfoMultiplier;

	public DynPitchVol()
	{
	}

	public DynPitchVol( int preset, int pitchRun, int pitchStart, int spinUp, int spinDown, int volumeRun, int volumeStart, int fadeIn, int fadeOut, int lfoType, int lfoRate, int lfoModPitch, int lfoModVolume, int countSpinUp )
	{
		Preset = preset;
		PitchRun = pitchRun;
		PitchStart = pitchStart;
		SpinUp = spinUp;
		SpinDown = spinDown;
		VolumeRun = volumeRun;
		VolumeStart = volumeStart;
		FadeIn = fadeIn;
		FadeOut = fadeOut;
		LfoType = lfoType;
		LfoRate = lfoRate;
		LfoModPitch = lfoModPitch;
		LfoModVolume = lfoModVolume;
		CountSpinUp = countSpinUp;
	}
}

[LinkEntityToClass( "ambient_generic" )]
public class AmbientGeneric : BaseEntity
{
	private const int SpawnFlagEverywhere = 1;
	private const int SpawnFlagSmallRadius = 2;
	private const int SpawnFlagMediumRadius = 4;
	private const int SpawnFlagLargeRadius = 8;
	private const int SpawnFlagStartSilent = 16;
	private const int SpawnFlagNotLooping = 32;

	private const int LfoSquare = 1;
	private const int LfoTriangle = 2;
	private const int LfoRandom = 3;

	private static readonly DynPitchVol[] Presets =
	[
		new( 1, 255, 75, 95, 95, 10, 1, 50, 95, 0, 0, 0, 0, 0 ),
		new( 2, 255, 85, 70, 88, 10, 1, 20, 88, 0, 0, 0, 0, 0 ),
		new( 3, 255, 100, 50, 75, 10, 1, 10, 75, 0, 0, 0, 0, 0 ),
		new( 4, 100, 100, 0, 0, 10, 1, 90, 90, 0, 0, 0, 0, 0 ),
		new( 5, 100, 100, 0, 0, 10, 1, 80, 80, 0, 0, 0, 0, 0 ),
		new( 6, 100, 100, 0, 0, 10, 1, 50, 70, 0, 0, 0, 0, 0 ),
		new( 7, 100, 100, 0, 0, 5, 1, 40, 50, 1, 50, 0, 10, 0 ),
		new( 8, 100, 100, 0, 0, 5, 1, 40, 50, 1, 150, 0, 10, 0 ),
		new( 9, 100, 100, 0, 0, 5, 1, 40, 50, 1, 750, 0, 10, 0 ),
		new( 10, 128, 100, 50, 75, 10, 1, 30, 40, 2, 8, 20, 0, 0 ),
		new( 11, 128, 100, 50, 75, 10, 1, 30, 40, 2, 25, 20, 0, 0 ),
		new( 12, 128, 100, 50, 75, 10, 1, 30, 40, 2, 70, 20, 0, 0 ),
		new( 13, 50, 50, 0, 0, 10, 1, 20, 50, 0, 0, 0, 0, 0 ),
		new( 14, 70, 70, 0, 0, 10, 1, 20, 50, 0, 0, 0, 0, 0 ),
		new( 15, 90, 90, 0, 0, 10, 1, 20, 50, 0, 0, 0, 0, 0 ),
		new( 16, 120, 120, 0, 0, 10, 1, 20, 50, 0, 0, 0, 0, 0 ),
		new( 17, 180, 180, 0, 0, 10, 1, 20, 50, 0, 0, 0, 0, 0 ),
		new( 18, 255, 255, 0, 0, 10, 1, 20, 50, 0, 0, 0, 0, 0 ),
		new( 19, 200, 75, 90, 90, 10, 1, 50, 90, 2, 100, 20, 0, 0 ),
		new( 20, 255, 75, 97, 90, 10, 1, 50, 90, 1, 40, 50, 0, 0 ),
		new( 21, 100, 100, 0, 0, 10, 1, 30, 50, 3, 15, 20, 0, 0 ),
		new( 22, 160, 160, 0, 0, 10, 1, 50, 50, 3, 500, 25, 0, 0 ),
		new( 23, 255, 75, 88, 0, 10, 1, 40, 0, 0, 0, 0, 0, 5 ),
		new( 24, 200, 20, 95, 70, 10, 1, 70, 70, 3, 20, 50, 0, 0 ),
		new( 25, 180, 100, 50, 60, 10, 1, 40, 60, 2, 90, 100, 100, 0 ),
		new( 26, 60, 60, 0, 0, 10, 1, 40, 70, 3, 80, 20, 50, 0 ),
		new( 27, 128, 90, 10, 10, 10, 1, 20, 40, 1, 5, 10, 20, 0 )
	];

	[Save( FieldType.Float )]
	public float Attenuation { get; set; }
	[Save( FieldType.Boolean )]
	public new bool Active { get; set; }
	[Save( FieldType.Boolean )]
	public bool Looping { get; set; }

	private DynPitchVol _dpv = new();

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool Save( Save save )
	{
		return base.Save( save ) && save.WriteFields( nameof( DynPitchVol ), _dpv, typeof( DynPitchVol ) );
	}

	public override bool Restore( Restore restore )
	{
		return base.Restore( restore ) && restore.ReadFields( nameof( DynPitchVol ), _dpv, typeof( DynPitchVol ) );
	}

	public override void Spawn()
	{
		if ( (SpawnFlags & SpawnFlagEverywhere) != 0 )
			Attenuation = Server.AttenuationNone;
		else if ( (SpawnFlags & SpawnFlagSmallRadius) != 0 )
			Attenuation = Server.AttenuationIdle;
		else if ( (SpawnFlags & SpawnFlagMediumRadius) != 0 )
			Attenuation = Server.AttenuationStatic;
		else if ( (SpawnFlags & SpawnFlagLargeRadius) != 0 )
			Attenuation = Server.AttenuationNormal;
		else
			Attenuation = Server.AttenuationStatic;

		if ( string.IsNullOrEmpty( Message ) )
		{
			Log.Error( $"EMPTY AMBIENT AT: {Origin.x:F6}, {Origin.y:F6}, {Origin.z:F6}" );
			NextThink = (float)(Server.Globals.Time + 0.1);
			SetThink( SubRemove );
			return;
		}

		Solid = Solid.Not;
		MoveType = MoveType.None;

		SetThink( RampThink );
		NextThink = 0f;

		SetUse( ToggleUse );

		Active = false;
		Looping = (SpawnFlags & SpawnFlagNotLooping) == 0;

		Precache();
	}

	public override void Precache()
	{
		var soundFile = Message;

		if ( !string.IsNullOrEmpty( soundFile ) && soundFile.Length > 1 )
		{
			if ( soundFile[0] != '!' )
				Server.PrecacheSound( soundFile );
		}

		InitModulationParms();

		if ( (SpawnFlags & SpawnFlagStartSilent) == 0 )
		{
			if ( Looping )
				Active = true;
		}

		if ( Active )
		{
			Sound.EmitAmbient( this, Origin, soundFile, (float)(_dpv.Volume * 0.01), Attenuation, Server.SoundSpawning, _dpv.Pitch );

			NextThink = (float)(Server.Globals.Time + 0.1);
		}
	}

	private void RampThink()
	{
		var soundFile = Message;
		var pitch = _dpv.Pitch;
		var volume = _dpv.Volume;
		var flags = 0;
		var changed = false;
		int previous;

		if ( _dpv.SpinUp == 0 && _dpv.SpinDown == 0 && _dpv.FadeIn == 0 && _dpv.FadeOut == 0 && _dpv.LfoType == 0 )
			return;

		if ( _dpv.SpinUp != 0 || _dpv.SpinDown != 0 )
		{
			previous = _dpv.PitchFraction >> 8;

			if ( _dpv.SpinUp > 0 )
				_dpv.PitchFraction += _dpv.SpinUp;
			else if ( _dpv.SpinDown > 0 )
				_dpv.PitchFraction -= _dpv.SpinDown;

			pitch = _dpv.PitchFraction >> 8;

			if ( pitch > _dpv.PitchRun )
			{
				pitch = _dpv.PitchRun;
				_dpv.SpinUp = 0;
			}

			if ( pitch < _dpv.PitchStart )
			{
				pitch = _dpv.PitchStart;
				_dpv.SpinDown = 0;

				Sound.EmitAmbient( this, Origin, soundFile, 0f, 0f, Server.SoundStop, 0 );

				return;
			}

			if ( pitch > 255 )
				pitch = 255;

			if ( pitch < 1 )
				pitch = 1;

			_dpv.Pitch = pitch;

			changed |= previous != pitch;
			flags |= Server.SoundChangePitch;
		}

		if ( _dpv.FadeIn != 0 || _dpv.FadeOut != 0 )
		{
			previous = _dpv.VolumeFraction >> 8;

			if ( _dpv.FadeIn > 0 )
				_dpv.VolumeFraction += _dpv.FadeIn;
			else if ( _dpv.FadeOut > 0 )
				_dpv.VolumeFraction -= _dpv.FadeOut;

			volume = _dpv.VolumeFraction >> 8;

			if ( volume > _dpv.VolumeRun )
			{
				volume = _dpv.VolumeRun;
				_dpv.FadeIn = 0;
			}

			if ( volume < _dpv.VolumeStart )
			{
				volume = _dpv.VolumeStart;
				_dpv.FadeOut = 0;

				Sound.EmitAmbient( this, Origin, soundFile, 0f, 0f, Server.SoundStop, 0 );

				return;
			}

			if ( volume > 100 )
				volume = 100;

			if ( volume < 1 )
				volume = 1;

			_dpv.Volume = volume;

			changed |= previous != volume;
			flags |= Server.SoundChangeVolume;
		}

		if ( _dpv.LfoType != 0 )
		{
			if ( _dpv.LfoFraction > 0x6fffffff )
				_dpv.LfoFraction = 0;

			_dpv.LfoFraction += _dpv.LfoRate;

			var position = _dpv.LfoFraction >> 8;

			if ( _dpv.LfoFraction < 0 )
			{
				_dpv.LfoFraction = 0;
				_dpv.LfoRate = Math.Abs( _dpv.LfoRate );
				position = 0;
			}
			else if ( position > 255 )
			{
				position = 255;
				_dpv.LfoFraction = 255 << 8;
				_dpv.LfoRate = -Math.Abs( _dpv.LfoRate );
			}

			switch ( _dpv.LfoType )
			{
				case LfoSquare:
					_dpv.LfoMultiplier = position < 128 ? 255 : 0;
					break;
				case LfoRandom:
					if ( position == 255 )
						_dpv.LfoMultiplier = Server.RandomLong( 0, 255 );

					break;
				default:
					_dpv.LfoMultiplier = position;
					break;
			}

			if ( _dpv.LfoModPitch != 0 )
			{
				previous = pitch;

				pitch += (_dpv.LfoMultiplier - 128) * _dpv.LfoModPitch / 100;

				if ( pitch > 255 )
					pitch = 255;

				if ( pitch < 1 )
					pitch = 1;

				changed |= previous != pitch;
				flags |= Server.SoundChangePitch;
			}

			if ( _dpv.LfoModVolume != 0 )
			{
				previous = volume;

				volume += (_dpv.LfoMultiplier - 128) * _dpv.LfoModVolume / 100;

				if ( volume > 100 )
					volume = 100;

				if ( volume < 0 )
					volume = 0;

				changed |= previous != volume;
				flags |= Server.SoundChangeVolume;
			}
		}

		if ( flags != 0 && changed )
		{
			if ( pitch == Server.PitchNormal )
				pitch = Server.PitchNormal + 1;

			Sound.EmitAmbient( this, Origin, soundFile, (float)(volume * 0.01), Attenuation, flags, pitch );
		}

		NextThink = (float)(Server.Globals.Time + 0.2);
	}

	private void InitModulationParms()
	{
		_dpv.VolumeRun = (int)(Health * 10f);

		if ( _dpv.VolumeRun > 100 )
			_dpv.VolumeRun = 100;

		if ( _dpv.VolumeRun < 0 )
			_dpv.VolumeRun = 0;

		if ( _dpv.Preset != 0 && _dpv.Preset <= Presets.Length )
		{
			_dpv = Presets[_dpv.Preset - 1] with { };

			if ( _dpv.SpinDown > 0 )
				_dpv.SpinDown = (101 - _dpv.SpinDown) * 64;

			if ( _dpv.SpinUp > 0 )
				_dpv.SpinUp = (101 - _dpv.SpinUp) * 64;

			_dpv.VolumeStart *= 10;
			_dpv.VolumeRun *= 10;

			if ( _dpv.FadeIn > 0 )
				_dpv.FadeIn = (101 - _dpv.FadeIn) * 64;

			if ( _dpv.FadeOut > 0 )
				_dpv.FadeOut = (101 - _dpv.FadeOut) * 64;

			_dpv.LfoRate *= 256;

			_dpv.FadeInSaved = _dpv.FadeIn;
			_dpv.FadeOutSaved = _dpv.FadeOut;
			_dpv.SpinUpSaved = _dpv.SpinUp;
			_dpv.SpinDownSaved = _dpv.SpinDown;
		}

		_dpv.FadeIn = _dpv.FadeInSaved;
		_dpv.FadeOut = 0;

		_dpv.Volume = _dpv.FadeIn != 0 ? _dpv.VolumeStart : _dpv.VolumeRun;

		_dpv.SpinUp = _dpv.SpinUpSaved;
		_dpv.SpinDown = 0;

		_dpv.Pitch = _dpv.SpinUp != 0 ? _dpv.PitchStart : _dpv.PitchRun;

		if ( _dpv.Pitch == 0 )
			_dpv.Pitch = Server.PitchNormal;

		_dpv.PitchFraction = _dpv.Pitch << 8;
		_dpv.VolumeFraction = _dpv.Volume << 8;

		_dpv.LfoFraction = 0;
		_dpv.LfoRate = Math.Abs( _dpv.LfoRate );

		_dpv.SpinCount = 1;

		if ( _dpv.CountSpinUp != 0 )
		{
			var pitchIncrement = (255 - _dpv.PitchStart) / _dpv.CountSpinUp;

			_dpv.PitchRun = _dpv.PitchStart + pitchIncrement;

			if ( _dpv.PitchRun > 255 )
				_dpv.PitchRun = 255;
		}

		if ( (_dpv.SpinUpSaved != 0 || _dpv.SpinDownSaved != 0 || (_dpv.LfoType != 0 && _dpv.LfoModPitch != 0)) && _dpv.Pitch == Server.PitchNormal )
			_dpv.Pitch = Server.PitchNormal + 1;
	}

	private void ToggleUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var soundFile = Message;

		if ( useType != UseType.Toggle )
		{
			if ( (Active && useType == UseType.On) || (!Active && useType == UseType.Off) )
				return;
		}

		if ( useType == UseType.Set && Active )
		{
			var fraction = value;

			if ( fraction > 1f )
				fraction = 1f;

			if ( fraction < 0f )
				fraction = 0.01f;

			_dpv.Pitch = (int)(fraction * 255f);

			Sound.EmitAmbient( this, Origin, soundFile, 0f, 0f, Server.SoundChangePitch, _dpv.Pitch );

			return;
		}

		if ( Active )
		{
			if ( _dpv.CountSpinUp != 0 )
			{
				if ( _dpv.SpinCount <= _dpv.CountSpinUp )
				{
					_dpv.SpinCount++;

					var pitchIncrement = (255 - _dpv.PitchStart) / _dpv.CountSpinUp;

					_dpv.SpinUp = _dpv.SpinUpSaved;
					_dpv.SpinDown = 0;

					_dpv.PitchRun = _dpv.PitchStart + (pitchIncrement * _dpv.SpinCount);

					if ( _dpv.PitchRun > 255 )
						_dpv.PitchRun = 255;

					NextThink = (float)(Server.Globals.Time + 0.1);
				}
			}
			else
			{
				Active = false;

				SpawnFlags |= SpawnFlagStartSilent;

				if ( _dpv.SpinDownSaved != 0 || _dpv.FadeOutSaved != 0 )
				{
					_dpv.SpinDown = _dpv.SpinDownSaved;
					_dpv.SpinUp = 0;

					_dpv.FadeOut = _dpv.FadeOutSaved;
					_dpv.FadeIn = 0;
					NextThink = (float)(Server.Globals.Time + 0.1);
				}
				else
				{
					Sound.EmitAmbient( this, Origin, soundFile, 0f, 0f, Server.SoundStop, 0 );
				}
			}
		}
		else
		{
			if ( Looping )
				Active = true;
			else
				Sound.EmitAmbient( this, Origin, soundFile, 0f, 0f, Server.SoundStop, 0 );

			InitModulationParms();

			Sound.EmitAmbient( this, Origin, soundFile, (float)(_dpv.Volume * 0.01), Attenuation, 0, _dpv.Pitch );

			NextThink = (float)(Server.Globals.Time + 0.1);
		}
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "preset":
				_dpv.Preset = Util.Atoi( value );
				return true;
			case "pitch":
				_dpv.PitchRun = Math.Clamp( Util.Atoi( value ), 0, 255 );
				return true;
			case "pitchstart":
				_dpv.PitchStart = Math.Clamp( Util.Atoi( value ), 0, 255 );
				return true;
			case "spinup":
				_dpv.SpinUp = Math.Clamp( Util.Atoi( value ), 0, 100 );

				if ( _dpv.SpinUp > 0 )
					_dpv.SpinUp = (101 - _dpv.SpinUp) * 64;

				_dpv.SpinUpSaved = _dpv.SpinUp;
				return true;
			case "spindown":
				_dpv.SpinDown = Math.Clamp( Util.Atoi( value ), 0, 100 );

				if ( _dpv.SpinDown > 0 )
					_dpv.SpinDown = (101 - _dpv.SpinDown) * 64;

				_dpv.SpinDownSaved = _dpv.SpinDown;
				return true;
			case "volstart":
				_dpv.VolumeStart = Math.Clamp( Util.Atoi( value ), 0, 10 ) * 10;
				return true;
			case "fadein":
				_dpv.FadeIn = Math.Clamp( Util.Atoi( value ), 0, 100 );

				if ( _dpv.FadeIn > 0 )
					_dpv.FadeIn = (101 - _dpv.FadeIn) * 64;

				_dpv.FadeInSaved = _dpv.FadeIn;
				return true;
			case "fadeout":
				_dpv.FadeOut = Math.Clamp( Util.Atoi( value ), 0, 100 );

				if ( _dpv.FadeOut > 0 )
					_dpv.FadeOut = (101 - _dpv.FadeOut) * 64;

				_dpv.FadeOutSaved = _dpv.FadeOut;
				return true;
			case "lfotype":
				_dpv.LfoType = Util.Atoi( value );

				if ( _dpv.LfoType > 4 )
					_dpv.LfoType = LfoTriangle;

				return true;
			case "lforate":
				_dpv.LfoRate = Math.Clamp( Util.Atoi( value ), 0, 1000 ) * 256;
				return true;
			case "lfomodpitch":
				_dpv.LfoModPitch = Math.Clamp( Util.Atoi( value ), 0, 100 );
				return true;
			case "lfomodvol":
				_dpv.LfoModVolume = Math.Clamp( Util.Atoi( value ), 0, 100 );
				return true;
			case "cspinup":
				_dpv.CountSpinUp = Math.Clamp( Util.Atoi( value ), 0, 100 );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}
}

[LinkEntityToClass( "env_sound" )]
public class EnvSound : PointEntity
{
	[Save( FieldType.Float )]
	public float Radius { get; set; }
	[Save( FieldType.Float )]
	public float RoomType { get; set; }

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "radius":
				Radius = Util.Atof( value );
				return true;
			case "roomtype":
				RoomType = Util.Atof( value );
				return true;
			default:
				return false;
		}
	}

	private bool InRange( Edict target, out float range )
	{
		var spot1 = Origin + ViewOffset;
		var spot2 = target.Origin + target.ViewOffset;
		var trace = Util.TraceLine( spot1, spot2, IgnoreMonsters, this );

		range = 0f;

		if ( (trace.InOpen && trace.InWater) || trace.Fraction != 1f )
			return false;

		var length = (trace.EndPos - spot1).Length;

		if ( Radius < length )
			return false;

		range = length;

		return true;
	}

	public override void Think()
	{
		var client = Server.FindClientInPvs( this );

		if ( Util.IsNullEnt( client ) )
		{
			NextThink = (float)(Server.Globals.Time + 0.75);
			return;
		}

		var player = (Player)client;
		float range;

		if ( !Util.IsNullEnt( player.SoundLast ) && player.SoundLast == this )
		{
			if ( player.SoundRoomType == 0f || player.SoundRange == 0f )
			{
				NextThink = (float)(Server.Globals.Time + 0.75);
				return;
			}

			if ( InRange( client, out range ) )
			{
				player.SoundRange = range;
				NextThink = (float)(Server.Globals.Time + 0.25);
				return;
			}

			player.SoundRange = 0f;
			player.SoundRoomType = 0f;
			NextThink = (float)(Server.Globals.Time + 0.75);
			return;
		}

		if ( InRange( client, out range ) )
		{
			if ( range < player.SoundRange || player.SoundRange == 0f )
			{
				player.SoundLast = this;
				player.SoundRoomType = RoomType;
				player.SoundRange = range;

				Server.MessageBegin( MessageDest.One, Server.RoomTypeMessage, default, client );
				Server.WriteShort( (short)RoomType );
				Server.MessageEnd();
			}
		}

		NextThink = (float)(Server.Globals.Time + 0.25);
	}

	public override void Spawn()
	{
		NextThink = (float)(Server.Globals.Time + Server.RandomFloat( 0f, 0.5f ));
	}
}

[LinkEntityToClass( "speaker" )]
public class Speaker : BaseEntity
{
	private const int SpawnFlagStartSilent = 1;

	private const float AnnounceMinutesMin = 0.25f;
	private const float AnnounceMinutesMax = 2.25f;

	private static readonly string[] PresetGroups =
	[
		"C1A0_",
		"C1A1_",
		"C1A2_",
		"C1A3_",
		"C1A4_",
		"C2A1_",
		"C2A2_",
		"C2A3_",
		"C2A4_",
		"C2A5_",
		"C3A1_",
		"C3A2_"
	];

	[Save( FieldType.Integer )]
	public int Preset { get; set; }

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override void Spawn()
	{
		if ( Preset == 0 && string.IsNullOrEmpty( Message ) )
		{
			Log.Error( $"SPEAKER with no Level/Sentence! at: {Origin.x:F6}, {Origin.y:F6}, {Origin.z:F6}" );
			NextThink = (float)(Server.Globals.Time + 0.1);
			SetThink( SubRemove );
			return;
		}

		Solid = Solid.Not;
		MoveType = MoveType.None;

		SetThink( SpeakerThink );
		NextThink = 0f;

		SetUse( ToggleUse );

		Precache();
	}

	public override void Precache()
	{
		if ( (SpawnFlags & SpawnFlagStartSilent) == 0 )
			NextThink = (float)(Server.Globals.Time + Server.RandomFloat( 5f, 15f ));
	}

	private void SpeakerThink()
	{
		var volume = (float)(Health * 0.1);
		var attenuation = 0.3f;
		var flags = 0;
		var pitch = 100;

		if ( Server.Globals.Time <= TalkMonster.TalkWaitTime )
		{
			NextThink = TalkMonster.TalkWaitTime + Server.RandomFloat( 5f, 10f );
			return;
		}

		var soundFile = Preset != 0 ? PresetGroups[Preset - 1] : Message;

		if ( soundFile[0] == '!' )
		{
			Sound.EmitAmbient( this, Origin, soundFile, volume, attenuation, flags, pitch );

			NextThink = 0f;
		}
		else
		{
			if ( Sentences.PlayRandom( this, soundFile, volume, attenuation, flags, pitch ) < 0 )
				Log.Info( $"Level Design Error!\nSPEAKER has bad sentence group name: {soundFile}" );

			NextThink = (float)(Server.Globals.Time + Server.RandomFloat( AnnounceMinutesMin * 60f, AnnounceMinutesMax * 60f ));

			TalkMonster.TalkWaitTime = (float)(Server.Globals.Time + 5);
		}
	}

	private void ToggleUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var active = NextThink > 0f;

		if ( useType != UseType.Toggle )
		{
			if ( (active && useType == UseType.On) || (!active && useType == UseType.Off) )
				return;
		}

		if ( useType == UseType.On )
		{
			NextThink = (float)(Server.Globals.Time + 0.1);
			return;
		}

		if ( useType == UseType.Off )
		{
			NextThink = 0f;
			return;
		}

		NextThink = active ? 0f : (float)(Server.Globals.Time + 0.1);
	}

	public override bool KeyValue( string key, string value )
	{
		if ( key == "preset" )
		{
			Preset = Util.Atoi( value );
			return true;
		}

		return base.KeyValue( key, value );
	}
}
