using System;
using Sandbox.Mounting;

namespace GoldSrc;

partial class SoundEngine
{
	private const string SoundExtension = ".vsnd";
	private const int RiffId = 0x46464952;
	private const int WaveId = 0x45564157;
	private const int CueId = 0x20657563;
	private const int RiffHeaderSize = 12;
	private const int ChunkHeaderSize = 8;

	private static bool HasCue( byte[] data )
	{
		if ( data is null || data.Length < RiffHeaderSize || BitConverter.ToInt32( data, 0 ) != RiffId || BitConverter.ToInt32( data, 8 ) != WaveId )
			return false;

		var position = RiffHeaderSize;

		while ( position + ChunkHeaderSize <= data.Length )
		{
			var length = BitConverter.ToInt32( data, position + 4 );

			if ( length < 0 )
				return false;

			if ( BitConverter.ToInt32( data, position ) == CueId )
				return true;

			position += ChunkHeaderSize + ((length + 1) & ~1);
		}

		return false;
	}

	private static bool LoadSound( Sfx sfx, bool stream = false )
	{
		var name = sfx.Name;

		if ( name[0] == '*' )
		{
			if ( !stream )
				return false;

			name = name.Length > 64 ? name[1..64] : name[1..];
		}

		if ( name[0] == '?' )
			return false;

		if ( sfx.File is { IsValid: true } )
			return true;

		var path = name[0] == '/' ? $"sound{name}" : $"sound/{name}";
		var loader = MountUtility.FindLoader( $"{Server.GetMountPath( path )}{SoundExtension}" );

		if ( loader is null )
		{
			path = name[0] == '/' ? name : $"/{name}";
			loader = MountUtility.FindLoader( $"{Server.GetMountPath( path )}{SoundExtension}" );
		}

		if ( loader is null )
		{
			Log.Trace( $"S_LoadSound: Couldn't load {path}" );
			return false;
		}

		var file = SoundFile.Load( loader.Path );

		if ( file is null || !file.IsValid )
		{
			Log.Trace( $"Failed loading {name}" );
			return false;
		}

		file.Preload();

		if ( !file.IsLoaded )
		{
			Log.Trace( $"Failed loading {name}" );
			return false;
		}

		if ( file.Channels != 1 )
		{
			Log.Trace( $"{name} is a stereo sample" );
			return false;
		}

		if ( sfx.Name[0] == '*' && file.BitsPerSample != 8 )
		{
			Log.Info( $"{name} is a 16 bit sample" );
			return false;
		}

		if ( sfx.Name[0] == '*' && file.Rate != MixSpeed )
		{
			Log.Info( $"{name} ignored, not stored at playback sample rate!" );
			return false;
		}

		sfx.File = file;
		sfx.Loop = HasCue( Server.LoadMountFile( path ) );
		sfx.Duration = file.Duration;
		sfx.Bits = file.BitsPerSample;
		sfx.Rate = file.Rate;

		return true;
	}

}
