using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudWeapon
{
	public const int FlagSelectOnEmpty = 1;

	public static HudWeapon MenuOnly { get; private set; } = new();

	public string Name = "";
	public int AmmoType;
	public int Ammo2Type;
	public int Max1;
	public int Max2;
	public int Slot;
	public int SlotPosition;
	public int Flags;
	public int Id;
	public int Clip;

	public int Active;
	public SpriteRect ActiveRect;
	public int Inactive;
	public SpriteRect InactiveRect;
	public int Ammo;
	public SpriteRect AmmoRect;
	public int Ammo2;
	public SpriteRect Ammo2Rect;
	public int Crosshair;
	public SpriteRect CrosshairRect;
	public int Autoaim;
	public SpriteRect AutoaimRect;
	public int ZoomedCrosshair;
	public SpriteRect ZoomedCrosshairRect;
	public int ZoomedAutoaim;
	public SpriteRect ZoomedAutoaimRect;
}

public sealed class WeaponsResource( Hud hud )
{
	public const int MaxWeapons = 32;
	public const int MaxWeaponSlots = 5;
	public const int MaxWeaponPositions = MaxWeaponSlots;
	public const int MaxAmmoTypes = 32;

	private readonly HudWeapon[] _weapons = new HudWeapon[MaxWeapons];
	private readonly HudWeapon[,] _slots = new HudWeapon[MaxWeaponSlots + 1, MaxWeaponPositions + 1];
	private readonly int[] _ammo = new int[MaxAmmoTypes];

	public int OldWeaponBits { get; set; }

	public void Init()
	{
		for ( var i = 0; i < MaxWeapons; i++ )
			_weapons[i] = new HudWeapon();

		Reset();
	}

	public void Reset()
	{
		OldWeaponBits = 0;

		Array.Clear( _slots );
		Array.Clear( _ammo );
	}

	public HudWeapon GetWeapon( int id ) => _weapons[id];

	public void AddWeapon( HudWeapon source )
	{
		var weapon = _weapons[source.Id];

		weapon.Name = source.Name;
		weapon.AmmoType = source.AmmoType;
		weapon.Ammo2Type = source.Ammo2Type;
		weapon.Max1 = source.Max1;
		weapon.Max2 = source.Max2;
		weapon.Slot = source.Slot;
		weapon.SlotPosition = source.SlotPosition;
		weapon.Flags = source.Flags;
		weapon.Id = source.Id;
		weapon.Clip = source.Clip;

		LoadWeaponSprites( weapon );
	}

	public void PickupWeapon( HudWeapon weapon ) => _slots[weapon.Slot, weapon.SlotPosition] = weapon;

	public void DropWeapon( HudWeapon weapon ) => _slots[weapon.Slot, weapon.SlotPosition] = null;

	public HudWeapon GetWeaponSlot( int slot, int position ) => _slots[slot, position];

	public void SetAmmo( int id, int count ) => _ammo[id] = count;

	public int CountAmmo( int id ) => id < 0 ? 0 : _ammo[id];

	public void LoadAllWeaponSprites()
	{
		foreach ( var weapon in _weapons )
		{
			if ( weapon.Id != 0 )
				LoadWeaponSprites( weapon );
		}
	}

	public bool HasAmmo( HudWeapon weapon )
	{
		if ( weapon is null )
			return false;

		if ( weapon.Max1 == -1 )
			return true;

		return weapon.AmmoType == -1 || weapon.Clip > 0 || CountAmmo( weapon.AmmoType ) != 0
			|| CountAmmo( weapon.Ammo2Type ) != 0 || (weapon.Flags & HudWeapon.FlagSelectOnEmpty) != 0;
	}

	private bool LoadSprite( ClientSprite[] list, string name, out int sprite, out SpriteRect rect )
	{
		var entry = Array.Find( list, x => x.Res == hud.Resolution() && x.Name == name );

		sprite = entry is null ? 0 : hud.Engine.SpriteLoad( $"sprites/{entry.Sprite}.spr" );
		rect = entry?.Rect ?? default;

		return entry is not null;
	}

	public void LoadWeaponSprites( HudWeapon weapon )
	{
		if ( weapon is null )
			return;

		weapon.ActiveRect = default;
		weapon.InactiveRect = default;
		weapon.AmmoRect = default;
		weapon.Ammo2Rect = default;
		weapon.Inactive = 0;
		weapon.Active = 0;
		weapon.Ammo = 0;
		weapon.Ammo2 = 0;

		var list = hud.Engine.SpriteGetList( $"sprites/{weapon.Name}.txt" );

		if ( list is null )
			return;

		if ( LoadSprite( list, "crosshair", out var sprite, out var rect ) )
			weapon.CrosshairRect = rect;

		weapon.Crosshair = sprite;

		if ( LoadSprite( list, "autoaim", out sprite, out rect ) )
			weapon.AutoaimRect = rect;

		weapon.Autoaim = sprite;

		if ( LoadSprite( list, "zoom", out sprite, out rect ) )
		{
			weapon.ZoomedCrosshair = sprite;
			weapon.ZoomedCrosshairRect = rect;
		}
		else
		{
			weapon.ZoomedCrosshair = weapon.Crosshair;
			weapon.ZoomedCrosshairRect = weapon.CrosshairRect;
		}

		if ( LoadSprite( list, "zoom_autoaim", out sprite, out rect ) )
		{
			weapon.ZoomedAutoaim = sprite;
			weapon.ZoomedAutoaimRect = rect;
		}
		else
		{
			weapon.ZoomedAutoaim = weapon.ZoomedCrosshair;
			weapon.ZoomedAutoaimRect = weapon.ZoomedCrosshairRect;
		}

		var history = hud.Ammo.History;

		if ( LoadSprite( list, "weapon", out sprite, out rect ) )
		{
			weapon.InactiveRect = rect;
			history.Gap = Math.Max( history.Gap, weapon.ActiveRect.Bottom - weapon.ActiveRect.Top );
		}

		weapon.Inactive = sprite;

		if ( LoadSprite( list, "weapon_s", out sprite, out rect ) )
			weapon.ActiveRect = rect;

		weapon.Active = sprite;

		if ( LoadSprite( list, "ammo", out sprite, out rect ) )
		{
			weapon.AmmoRect = rect;
			history.Gap = Math.Max( history.Gap, weapon.ActiveRect.Bottom - weapon.ActiveRect.Top );
		}

		weapon.Ammo = sprite;

		if ( LoadSprite( list, "ammo2", out sprite, out rect ) )
		{
			weapon.Ammo2Rect = rect;
			history.Gap = Math.Max( history.Gap, weapon.ActiveRect.Bottom - weapon.ActiveRect.Top );
		}

		weapon.Ammo2 = sprite;
	}

	public HudWeapon GetFirstPos( int slot )
	{
		for ( var i = 0; i < MaxWeaponPositions; i++ )
		{
			if ( _slots[slot, i] is { } weapon && HasAmmo( weapon ) )
				return weapon;
		}

		return null;
	}

	public HudWeapon GetNextActivePos( int slot, int position )
	{
		if ( position >= MaxWeaponPositions || slot >= MaxWeaponSlots )
			return null;

		var weapon = _slots[slot, position + 1];

		return weapon is null || !HasAmmo( weapon ) ? GetNextActivePos( slot, position + 1 ) : weapon;
	}

	public bool GetAmmoPicFromWeapon( int ammoId, out int sprite, out SpriteRect rect )
	{
		foreach ( var weapon in _weapons )
		{
			if ( weapon.AmmoType == ammoId )
			{
				sprite = weapon.Ammo;
				rect = weapon.AmmoRect;
				return true;
			}

			if ( weapon.Ammo2Type == ammoId )
			{
				sprite = weapon.Ammo2;
				rect = weapon.Ammo2Rect;
				return true;
			}
		}

		sprite = 0;
		rect = default;

		return false;
	}
}
