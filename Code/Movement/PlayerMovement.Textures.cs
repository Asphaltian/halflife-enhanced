using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

partial class PlayerMovement
{
	private const int MaxTextures = 512;
	private const int TextureNameMax = 13;
	private const int MaxLineLength = 511;

	private const char TextureConcrete = 'C';
	private const char TextureMetal = 'M';
	private const char TextureDirt = 'D';
	private const char TextureVent = 'V';
	private const char TextureGrate = 'G';
	private const char TextureTile = 'T';
	private const char TextureSlosh = 'S';

	private enum Step
	{
		Concrete,
		Metal,
		Dirt,
		Vent,
		Grate,
		Tile,
		Slosh,
		Wade,
		Ladder
	}

	private static readonly string[] StepSoundNames = ["step", "metal", "dirt", "duct", "grate", "tile", "slosh", "wade", "ladder"];
	private static readonly int[] StepSoundOrder = [1, 3, 2, 4];
	private static readonly int[] WadeSoundOrder = [1, 2, 3, 4];

	private readonly List<(string Name, char Type)> _textures = [];
	private bool _textureTypeInit;
	private int _skipStep;

	private void InitTextureTypes()
	{
		if ( _textureTypeInit )
			return;

		var file = _pmove.LoadFile( "sound/materials.txt" );
		if ( file is null )
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

			j = Math.Min( j, TextureNameMax - 1 + i );

			_textures.Add( (line[i..j], type) );
		}

		SortTextures();
		_textureTypeInit = true;
	}

	private void SortTextures()
	{
		for ( var i = 0; i < _textures.Count; i++ )
		{
			for ( var j = i + 1; j < _textures.Count; j++ )
			{
				if ( Strnicmp( _textures[i].Name, _textures[j].Name, int.MaxValue ) > 0 )
					(_textures[i], _textures[j]) = (_textures[j], _textures[i]);
			}
		}
	}

	private static int Strnicmp( string a, string b, int count )
	{
		for ( var i = 0; i < count; i++ )
		{
			var first = i < a.Length ? char.ToLowerInvariant( a[i] ) : '\0';
			var second = i < b.Length ? char.ToLowerInvariant( b[i] ) : '\0';

			if ( first != second )
				return first - second;

			if ( first == '\0' )
				break;
		}

		return 0;
	}

	public char FindTextureType( string name )
	{
		var left = 0;
		var right = _textures.Count - 1;

		while ( left <= right )
		{
			var pivot = (left + right) / 2;

			var value = Strnicmp( name, _textures[pivot].Name, TextureNameMax - 1 );

			if ( value == 0 )
				return _textures[pivot].Type;

			if ( value > 0 )
				left = pivot + 1;
			else
				right = pivot - 1;
		}

		return TextureConcrete;
	}

	private void PlayStepSound( Step step, float volume )
	{
		_pmove.StepLeft = _pmove.StepLeft == 0 ? 1 : 0;

		if ( !_pmove.RunFuncs )
			return;

		var random = _pmove.RandomLong( 0, 1 ) + (_pmove.StepLeft * 2);

		if ( _pmove.Multiplayer && _pmove.MoveVars.Footsteps == 0 )
			return;

		var horizontal = new Vector3( _pmove.Velocity.x, _pmove.Velocity.y, 0f );

		if ( _pmove.Multiplayer && !_onLadder && MathLib.Length( horizontal ) <= 220f )
			return;

		var order = StepSoundOrder;

		switch ( step )
		{
			case Step.Tile:
				if ( _pmove.RandomLong( 0, 4 ) == 0 )
				{
					PlayStep( "player/pl_tile5.wav", volume );
					return;
				}

				break;

			case Step.Wade:
				if ( _skipStep == 0 )
				{
					_skipStep++;
					return;
				}

				if ( _skipStep++ == 3 )
					_skipStep = 0;

				order = WadeSoundOrder;
				break;
		}

		PlayStep( $"player/pl_{StepSoundNames[(int)step]}{order[random]}.wav", volume );
	}

	private void PlayStep( string sample, float volume )
	{
		_pmove.PlaySound( SoundChannel.Body, sample, volume, Server.AttenuationNormal, 0, Server.PitchNormal );
	}

	private static Step MapTextureTypeStepType( char textureType )
	{
		return textureType switch
		{
			TextureMetal => Step.Metal,
			TextureDirt => Step.Dirt,
			TextureVent => Step.Vent,
			TextureGrate => Step.Grate,
			TextureTile => Step.Tile,
			TextureSlosh => Step.Slosh,
			_ => Step.Concrete
		};
	}

	private void CatagorizeTextureType()
	{
		var start = _pmove.Origin;
		var end = _pmove.Origin;
		end.z -= 64f;

		_pmove.TextureName = string.Empty;
		_pmove.TextureType = TextureConcrete;

		var name = _pmove.TraceTexture( _pmove.OnGround, start, end );
		if ( name is null )
			return;

		if ( name.Length > 0 && name[0] is '-' or '+' )
			name = name.Length >= 2 ? name[2..] : string.Empty;

		if ( name.Length > 0 && name[0] is '{' or '!' or '~' or ' ' )
			name = name[1..];

		if ( name.Length > TextureNameMax - 1 )
			name = name[..(TextureNameMax - 1)];

		_pmove.TextureName = name;
		_pmove.TextureType = FindTextureType( name );
	}

	private void UpdateStepSound()
	{
		if ( _pmove.TimeStepSound > 0 )
			return;

		if ( (_pmove.Flags & EntityFlags.Frozen) == EntityFlags.Frozen )
			return;

		CatagorizeTextureType();

		var speed = MathLib.Length( _pmove.Velocity );

		var ladder = _pmove.MoveType == MoveType.Fly && (_pmove.Flags & EntityFlags.ImmuneLava) != EntityFlags.ImmuneLava;

		float walkSpeed;
		float runSpeed;
		int duckTime;

		if ( (_pmove.Flags & EntityFlags.Ducking) == EntityFlags.Ducking || ladder )
		{
			walkSpeed = 60f;
			runSpeed = 80f;
			duckTime = 100;
		}
		else
		{
			walkSpeed = 120f;
			runSpeed = 210f;
			duckTime = 0;
		}

		if ( !(ladder || _pmove.OnGround != -1) || !(MathLib.Length( _pmove.Velocity ) > 0f) || !(speed >= walkSpeed || _pmove.TimeStepSound == 0) )
			return;

		var walking = speed < runSpeed;

		var height = PlayerMove.PlayerMaxs[_pmove.UseHull].z - PlayerMove.PlayerMins[_pmove.UseHull].z;

		var knee = _pmove.Origin;
		var feet = _pmove.Origin;

		knee.z = (float)(_pmove.Origin.z - (0.3 * height));
		feet.z = (float)(_pmove.Origin.z - (0.5 * height));

		Step step;
		float volume;

		if ( ladder )
		{
			step = Step.Ladder;
			volume = 0.35f;
			_pmove.TimeStepSound = 350;
		}
		else if ( _pmove.PointContents( knee ) == (int)Contents.Water )
		{
			step = Step.Wade;
			volume = 0.65f;
			_pmove.TimeStepSound = 600;
		}
		else if ( _pmove.PointContents( feet ) == (int)Contents.Water )
		{
			step = Step.Slosh;
			volume = walking ? 0.2f : 0.5f;
			_pmove.TimeStepSound = walking ? 400 : 300;
		}
		else
		{
			step = MapTextureTypeStepType( _pmove.TextureType );

			switch ( _pmove.TextureType )
			{
				case TextureDirt:
					volume = walking ? 0.25f : 0.55f;
					break;
				case TextureVent:
					volume = walking ? 0.4f : 0.7f;
					break;
				default:
					volume = walking ? 0.2f : 0.5f;
					break;
			}

			_pmove.TimeStepSound = walking ? 400 : 300;
		}

		_pmove.TimeStepSound += duckTime;

		if ( (_pmove.Flags & EntityFlags.Ducking) == EntityFlags.Ducking )
			volume = (float)(volume * 0.35);

		PlayStepSound( step, volume );
	}

	private void PlayWaterSounds()
	{
		if ( (_pmove.OldWaterLevel == 0 && _pmove.WaterLevel != 0) || (_pmove.OldWaterLevel != 0 && _pmove.WaterLevel == 0) )
			PlayWadeSound();
	}

	private void PlayWadeSound()
	{
		_pmove.PlaySound( SoundChannel.Body, $"player/pl_wade{_pmove.RandomLong( 0, 3 ) + 1}.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
	}
}
