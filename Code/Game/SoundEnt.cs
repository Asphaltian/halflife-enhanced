using GoldSrc;

namespace HalfLife;

public sealed class WorldSound
{
	public const int None = 0;
	public const int Combat = 1 << 0;
	public const int World = 1 << 1;
	public const int Player = 1 << 2;
	public const int Carcass = 1 << 3;
	public const int Meat = 1 << 4;
	public const int Danger = 1 << 5;
	public const int Garbage = 1 << 6;
	public const int All = unchecked((int)0xFFFFFFFF);

	public Vector3 Origin;
	public int Type;
	public int Volume;
	public float ExpireTime;
	public int Next;
	public int NextAudible;

	public bool IsSound => (Type & (Combat | World | Player | Danger)) != 0;

	public bool IsScent => (Type & (Carcass | Meat | Garbage)) != 0;

	public void Clear()
	{
		Origin = Vector3.Zero;
		Type = 0;
		Volume = 0;
		ExpireTime = 0f;
		Next = SoundEnt.ListEmpty;
		NextAudible = 0;
	}

	public void Reset()
	{
		Origin = Vector3.Zero;
		Type = 0;
		Volume = 0;
		Next = SoundEnt.ListEmpty;
	}
}

[LinkEntityToClass( "soundent" )]
public class SoundEnt : BaseEntity
{
	public const int MaxWorldSounds = 64;
	public const int ListEmpty = -1;
	public const int ListTypeFree = 1;
	public const int ListTypeActive = 2;
	public const float NeverExpire = -1f;

	public static SoundEnt Current { get; set; }

	private readonly WorldSound[] _soundPool = new WorldSound[MaxWorldSounds];

	private int _freeSound;
	private int _activeSound;
	private int _lastActiveSounds;
	private bool _showReport;

	public override ObjectCaps ObjectCaps => ObjectCaps.DontSave;

	public bool IsEmpty => _activeSound == ListEmpty;

	public override void Spawn()
	{
		Solid = Solid.Not;
		Initialize();

		NextThink = Server.Globals.Time + 1f;
	}

	public override void Think()
	{
		NextThink = (float)(Server.Globals.Time + 0.3);

		var previous = ListEmpty;
		var sound = _activeSound;

		while ( sound != ListEmpty )
		{
			if ( _soundPool[sound].ExpireTime <= Server.Globals.Time && _soundPool[sound].ExpireTime != NeverExpire )
			{
				var next = _soundPool[sound].Next;

				FreeSound( sound, previous );

				sound = next;
			}
			else
			{
				previous = sound;
				sound = _soundPool[sound].Next;
			}
		}

		if ( _showReport )
		{
			Log.Info( $"Soundlist: {SoundsInList( ListTypeActive )} / {SoundsInList( ListTypeFree )}  ({SoundsInList( ListTypeActive ) - _lastActiveSounds})" );
			_lastActiveSounds = SoundsInList( ListTypeActive );
		}
	}

	public static void FreeSound( int sound, int previous )
	{
		if ( Current is not { } soundEnt )
			return;

		if ( previous != ListEmpty )
			soundEnt._soundPool[previous].Next = soundEnt._soundPool[sound].Next;
		else
			soundEnt._activeSound = soundEnt._soundPool[sound].Next;

		soundEnt._soundPool[sound].Next = soundEnt._freeSound;
		soundEnt._freeSound = sound;
	}

	public int AllocSound()
	{
		if ( _freeSound == ListEmpty )
		{
			Log.Info( "Free Sound List is full!" );
			return ListEmpty;
		}

		var newSound = _freeSound;

		_freeSound = _soundPool[_freeSound].Next;
		_soundPool[newSound].Next = _activeSound;
		_activeSound = newSound;

		return newSound;
	}

	public static void InsertSound( int type, Vector3 origin, int volume, float duration )
	{
		if ( Current is not { } soundEnt )
			return;

		var sound = soundEnt.AllocSound();

		if ( sound == ListEmpty )
		{
			Log.Info( "Could not AllocSound() for InsertSound() (DLL)" );
			return;
		}

		soundEnt._soundPool[sound].Origin = origin;
		soundEnt._soundPool[sound].Type = type;
		soundEnt._soundPool[sound].Volume = volume;
		soundEnt._soundPool[sound].ExpireTime = soundEnt.Server.Globals.Time + duration;
	}

	public void Initialize()
	{
		_freeSound = 0;
		_activeSound = ListEmpty;

		for ( var i = 0; i < MaxWorldSounds; i++ )
		{
			_soundPool[i] ??= new WorldSound();
			_soundPool[i].Clear();
			_soundPool[i].Next = i + 1;
		}

		_soundPool[MaxWorldSounds - 1].Next = ListEmpty;

		for ( var i = 0; i < Server.MaxClients; i++ )
		{
			var sound = Current.AllocSound();

			if ( sound == ListEmpty )
			{
				Log.Info( "Could not AllocSound() for Client Reserve! (DLL)" );
				return;
			}

			Current._soundPool[sound].ExpireTime = NeverExpire;
		}

		_showReport = Cvar.GetFloat( "displaysoundlist" ) == 1f;
	}

	public int SoundsInList( int listType )
	{
		int sound;

		if ( listType == ListTypeFree )
		{
			sound = _freeSound;
		}
		else if ( listType == ListTypeActive )
		{
			sound = _activeSound;
		}
		else
		{
			Log.Info( "Unknown Sound List Type!" );
			return 0;
		}

		var count = 0;

		for ( ; sound != ListEmpty; sound = _soundPool[sound].Next )
			count++;

		return count;
	}

	public static int ActiveList => Current?._activeSound ?? ListEmpty;

	public static int FreeList => Current?._freeSound ?? ListEmpty;

	public static WorldSound SoundPointerForIndex( int index )
	{
		if ( Current is not { } soundEnt )
			return null;

		if ( index > MaxWorldSounds - 1 )
		{
			Log.Info( "SoundPointerForIndex() - Index too large!" );
			return null;
		}

		if ( index < 0 )
		{
			Log.Info( "SoundPointerForIndex() - Index < 0!" );
			return null;
		}

		return soundEnt._soundPool[index];
	}

	public static int ClientSoundIndex( Edict client ) => client.Index - 1;
}
