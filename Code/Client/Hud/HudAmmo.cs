using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudAmmo( Hud hud ) : HudBase( hud )
{
	private float _fade;
	private HudWeapon _weapon;
	private HudWeapon _activeSelection;
	private HudWeapon _lastSelection;
	private int _bucket0;
	private int _selection;
	private int _bucketHeight;
	private int _bucketWidth;
	private int _ammoBarHeight;
	private int _ammoBarWidth;

	public WeaponsResource Weapons { get; } = new( hud );

	public HistoryResource History { get; } = new( hud );

	public int WeaponSelect { get; set; }

	public override void Init()
	{
		Hud.AddHudElem( this );

		Hud.Dll.HookUserMsg( "CurWeapon", MsgFuncCurWeapon );
		Hud.Dll.HookUserMsg( "WeaponList", MsgFuncWeaponList );
		Hud.Dll.HookUserMsg( "AmmoPickup", MsgFuncAmmoPickup );
		Hud.Dll.HookUserMsg( "WeapPickup", MsgFuncWeapPickup );
		Hud.Dll.HookUserMsg( "ItemPickup", MsgFuncItemPickup );
		Hud.Dll.HookUserMsg( "HideWeapon", MsgFuncHideWeapon );
		Hud.Dll.HookUserMsg( "AmmoX", MsgFuncAmmoX );

		Weapons.Init();

		Reset();

		Flags |= Active;
	}

	public override void Reset()
	{
		_fade = 0f;
		Flags |= Active;

		_activeSelection = null;
		Hud.HideHudDisplay = 0;

		Weapons.Reset();
		History.Reset();
	}

	public override void VidInit()
	{
		_bucket0 = Hud.GetSpriteIndex( "bucket1" );
		_selection = Hud.GetSpriteIndex( "selection" );

		var bucket = Hud.GetSpriteRect( _bucket0 );

		_bucketWidth = bucket.Right - bucket.Left;
		_bucketHeight = bucket.Bottom - bucket.Top;

		History.Gap = Math.Max( History.Gap, bucket.Bottom - bucket.Top );

		Weapons.LoadAllWeaponSprites();

		var scale = Hud.Resolution() switch
		{
			2560 => 4,
			1280 => 3,
			640 => 2,
			_ => 1
		};

		_ammoBarWidth = 10 * scale;
		_ammoBarHeight = 2 * scale;
	}

	public override void Think()
	{
		if ( Hud.PlayerDead )
			return;

		if ( Hud.WeaponBits != Weapons.OldWeaponBits )
		{
			Weapons.OldWeaponBits = Hud.WeaponBits;

			for ( var i = WeaponsResource.MaxWeapons - 1; i > 0; i-- )
			{
				var weapon = Weapons.GetWeapon( i );

				if ( (Hud.WeaponBits & (1 << weapon.Id)) != 0 )
					Weapons.PickupWeapon( weapon );
				else
					Weapons.DropWeapon( weapon );
			}
		}

		if ( _activeSelection is null )
			return;

		if ( (Hud.KeyBits & InButtons.Attack) != InButtons.Attack )
			return;

		if ( _activeSelection != HudWeapon.MenuOnly )
		{
			Engine.ServerCmd( _activeSelection.Name );
			WeaponSelect = _activeSelection.Id;
		}

		_lastSelection = _activeSelection;
		_activeSelection = null;
		Hud.KeyBits &= ~InButtons.Attack;

		Engine.PlaySoundByName( "common/wpn_select.wav", 1f );
	}

	private void SelectSlot( int slot, bool advance, int direction )
	{
		if ( Hud.Menu.MenuDisplayed && !advance && direction == 1 )
		{
			Hud.Menu.SelectMenuItem( slot + 1 );
			return;
		}

		if ( slot > WeaponsResource.MaxWeaponSlots )
			return;

		if ( Hud.PlayerDead || (Hud.HideHudDisplay & (Hud.HideWeapons | Hud.HideAll)) != 0 )
			return;

		if ( (Hud.WeaponBits & (1 << Hud.WeaponSuit)) == 0 )
			return;

		if ( (Hud.WeaponBits & ~(1 << Hud.WeaponSuit)) == 0 )
			return;

		HudWeapon weapon = null;
		var fastSwitch = Cvar.GetFloat( "hud_fastswitch" ) != 0f;

		if ( _activeSelection is null || _activeSelection == HudWeapon.MenuOnly || slot != _activeSelection.Slot )
		{
			Engine.PlaySoundByName( "common/wpn_hudon.wav", 1f );

			weapon = Weapons.GetFirstPos( slot );

			if ( weapon is not null && fastSwitch && Weapons.GetNextActivePos( weapon.Slot, weapon.SlotPosition ) is null )
			{
				Engine.ServerCmd( weapon.Name );
				WeaponSelect = weapon.Id;
				return;
			}
		}
		else
		{
			Engine.PlaySoundByName( "common/wpn_moveselect.wav", 1f );

			weapon = Weapons.GetNextActivePos( _activeSelection.Slot, _activeSelection.SlotPosition ) ?? Weapons.GetFirstPos( slot );
		}

		if ( weapon is null )
			_activeSelection = fastSwitch ? null : HudWeapon.MenuOnly;
		else
			_activeSelection = weapon;
	}

	private bool MsgFuncAmmoX( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var index = message.ReadByte();
		var count = message.ReadByte();

		Weapons.SetAmmo( index, Math.Abs( count ) );

		return true;
	}

	private bool MsgFuncAmmoPickup( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var index = message.ReadByte();
		var count = message.ReadByte();

		History.AddToHistory( HistorySlot.Ammo, index, Math.Abs( count ) );

		return true;
	}

	private bool MsgFuncWeapPickup( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		History.AddToHistory( HistorySlot.Weapon, message.ReadByte() );

		return true;
	}

	private bool MsgFuncItemPickup( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		History.AddToHistory( HistorySlot.Item, message.ReadString() );

		return true;
	}

	private bool MsgFuncHideWeapon( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		Hud.HideHudDisplay = message.ReadByte();

		if ( (Hud.HideHudDisplay & (Hud.HideWeapons | Hud.HideAll)) != 0 )
		{
			_activeSelection = null;
			Engine.SetCrosshair( 0, default, 0, 0, 0 );
		}
		else if ( _weapon is not null )
		{
			Engine.SetCrosshair( _weapon.Crosshair, _weapon.CrosshairRect, 255, 255, 255 );
		}

		return true;
	}

	private bool MsgFuncCurWeapon( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var state = message.ReadByte();
		var id = message.ReadChar();
		var clip = message.ReadChar();
		var onTarget = state > 1;

		if ( id < 1 )
		{
			Engine.SetCrosshair( 0, default, 0, 0, 0 );
			return false;
		}

		if ( Hud.Dll.User1 != HudSpectator.ObsInEye )
		{
			if ( id == -1 && clip == -1 )
			{
				Hud.PlayerDead = true;
				_activeSelection = null;
				return true;
			}

			Hud.PlayerDead = false;
		}

		var weapon = Weapons.GetWeapon( id );

		weapon.Clip = clip < -1 ? Math.Abs( clip ) : clip;

		if ( state == 0 )
			return true;

		_weapon = weapon;

		if ( Hud.Fov >= 90 )
		{
			if ( onTarget && _weapon.Autoaim != 0 )
				Engine.SetCrosshair( _weapon.Autoaim, _weapon.AutoaimRect, 255, 255, 255 );
			else
				Engine.SetCrosshair( _weapon.Crosshair, _weapon.CrosshairRect, 255, 255, 255 );
		}
		else if ( onTarget && _weapon.ZoomedAutoaim != 0 )
		{
			Engine.SetCrosshair( _weapon.ZoomedAutoaim, _weapon.ZoomedAutoaimRect, 255, 255, 255 );
		}
		else
		{
			Engine.SetCrosshair( _weapon.ZoomedCrosshair, _weapon.ZoomedCrosshairRect, 255, 255, 255 );
		}

		_fade = 200f;
		Flags |= Active;

		return true;
	}

	private bool MsgFuncWeaponList( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var weapon = new HudWeapon { Name = message.ReadString() };

		if ( weapon.Name.Length > 127 )
			weapon.Name = weapon.Name[..127];

		weapon.AmmoType = message.ReadChar();
		weapon.Max1 = message.ReadByte();

		if ( weapon.Max1 == 255 )
			weapon.Max1 = -1;

		weapon.Ammo2Type = message.ReadChar();
		weapon.Max2 = message.ReadByte();

		if ( weapon.Max2 == 255 )
			weapon.Max2 = -1;

		weapon.Slot = message.ReadChar();
		weapon.SlotPosition = message.ReadChar();
		weapon.Id = message.ReadChar();
		weapon.Flags = message.ReadByte();
		weapon.Clip = 0;

		if ( weapon.Id < 0 || weapon.Id >= WeaponsResource.MaxWeapons )
			return false;

		if ( weapon.Slot < 0 || weapon.Slot >= WeaponsResource.MaxWeaponSlots + 1 )
			return false;

		if ( weapon.SlotPosition < 0 || weapon.SlotPosition >= WeaponsResource.MaxWeaponPositions + 1 )
			return false;

		if ( weapon.AmmoType < -1 || weapon.AmmoType >= WeaponsResource.MaxAmmoTypes )
			return false;

		if ( weapon.Ammo2Type < -1 || weapon.Ammo2Type >= WeaponsResource.MaxAmmoTypes )
			return false;

		if ( weapon.AmmoType >= 0 && weapon.Max1 == 0 )
			return false;

		if ( weapon.Ammo2Type >= 0 && weapon.Max2 == 0 )
			return false;

		Weapons.AddWeapon( weapon );

		return true;
	}

	[ConVar( "hud_drawhistory_time" )]
	private static float DrawHistoryTime { get; set; } = 5f;

	[ConVar( "hud_fastswitch" )]
	private static float FastSwitch { get; set; } = 0f;

	private static HudAmmo Current => (ClientDll.Loaded as HalfLifeClientDll)?.Hud?.Ammo;

	[ConCmd( "slot1" )]
	private static void Slot1() => Current?.UserCmdSlot1();

	[ConCmd( "slot2" )]
	private static void Slot2() => Current?.UserCmdSlot2();

	[ConCmd( "slot3" )]
	private static void Slot3() => Current?.UserCmdSlot3();

	[ConCmd( "slot4" )]
	private static void Slot4() => Current?.UserCmdSlot4();

	[ConCmd( "slot5" )]
	private static void Slot5() => Current?.UserCmdSlot5();

	[ConCmd( "slot6" )]
	private static void Slot6() => Current?.UserCmdSlot6();

	[ConCmd( "slot7" )]
	private static void Slot7() => Current?.UserCmdSlot7();

	[ConCmd( "slot8" )]
	private static void Slot8() => Current?.UserCmdSlot8();

	[ConCmd( "slot9" )]
	private static void Slot9() => Current?.UserCmdSlot9();

	[ConCmd( "slot10" )]
	private static void Slot10() => Current?.UserCmdSlot10();

	[ConCmd( "cancelselect" )]
	private static void CancelSelect() => Current?.UserCmdClose();

	[ConCmd( "invnext" )]
	private static void NextWeapon() => Current?.UserCmdNextWeapon();

	[ConCmd( "invprev" )]
	private static void PreviousWeapon() => Current?.UserCmdPrevWeapon();

	private void SlotInput( int slot )
	{
		if ( Hud.ViewPort is { } viewPort && viewPort.SlotInput( slot ) )
			return;

		SelectSlot( slot, false, 1 );
	}

	private void UserCmdSlot1() => SlotInput( 0 );

	private void UserCmdSlot2() => SlotInput( 1 );

	private void UserCmdSlot3() => SlotInput( 2 );

	private void UserCmdSlot4() => SlotInput( 3 );

	private void UserCmdSlot5() => SlotInput( 4 );

	private void UserCmdSlot6() => SlotInput( 5 );

	private void UserCmdSlot7() => SlotInput( 6 );

	private void UserCmdSlot8() => SlotInput( 7 );

	private void UserCmdSlot9() => SlotInput( 8 );

	private void UserCmdSlot10() => SlotInput( 9 );

	private void UserCmdClose()
	{
		if ( _activeSelection is null )
		{
			Client.ClientCmd( "escape" );
			return;
		}

		_lastSelection = _activeSelection;
		_activeSelection = null;

		Engine.PlaySoundByName( "common/wpn_hudoff.wav", 1f );
	}

	private void UserCmdNextWeapon()
	{
		if ( Hud.PlayerDead || (Hud.HideHudDisplay & (Hud.HideWeapons | Hud.HideAll)) != 0 )
			return;

		if ( _activeSelection is null || _activeSelection == HudWeapon.MenuOnly )
			_activeSelection = _weapon;

		var position = 0;
		var slot = 0;

		if ( _activeSelection is not null )
		{
			position = _activeSelection.SlotPosition + 1;
			slot = _activeSelection.Slot;
		}

		for ( var loop = 0; loop <= 1; loop++ )
		{
			for ( ; slot < WeaponsResource.MaxWeaponSlots; slot++ )
			{
				for ( ; position < WeaponsResource.MaxWeaponPositions; position++ )
				{
					var weapon = Weapons.GetWeaponSlot( slot, position );

					if ( weapon is not null && Weapons.HasAmmo( weapon ) )
					{
						_activeSelection = weapon;
						return;
					}
				}

				position = 0;
			}

			slot = 0;
		}

		_activeSelection = null;
	}

	private void UserCmdPrevWeapon()
	{
		if ( Hud.PlayerDead || (Hud.HideHudDisplay & (Hud.HideWeapons | Hud.HideAll)) != 0 )
			return;

		if ( _activeSelection is null || _activeSelection == HudWeapon.MenuOnly )
			_activeSelection = _weapon;

		var position = WeaponsResource.MaxWeaponPositions - 1;
		var slot = WeaponsResource.MaxWeaponSlots - 1;

		if ( _activeSelection is not null )
		{
			position = _activeSelection.SlotPosition - 1;
			slot = _activeSelection.Slot;
		}

		for ( var loop = 0; loop <= 1; loop++ )
		{
			for ( ; slot >= 0; slot-- )
			{
				for ( ; position >= 0; position-- )
				{
					var weapon = Weapons.GetWeaponSlot( slot, position );

					if ( weapon is not null && Weapons.HasAmmo( weapon ) )
					{
						_activeSelection = weapon;
						return;
					}
				}

				position = WeaponsResource.MaxWeaponPositions - 1;
			}

			slot = WeaponsResource.MaxWeaponSlots - 1;
		}

		_activeSelection = null;
	}

	public override void Draw( float time )
	{
		if ( (Hud.WeaponBits & (1 << Hud.WeaponSuit)) == 0 )
			return;

		if ( (Hud.HideHudDisplay & (Hud.HideWeapons | Hud.HideAll)) != 0 )
			return;

		DrawWeaponList( time );

		History.DrawAmmoHistory( time );

		if ( (Flags & Active) == 0 || _weapon is null )
			return;

		if ( _weapon.AmmoType < 0 && _weapon.Ammo2Type < 0 )
			return;

		var flags = Hud.DrawZero;
		var ammoWidth = Hud.GetSpriteRect( Hud.NumberZero ).Right - Hud.GetSpriteRect( Hud.NumberZero ).Left;
		var a = Math.Max( Hud.MinAlpha, (int)_fade );

		if ( _fade > 0f )
			_fade -= (float)(Hud.TimeDelta * 20);

		Hud.UnpackRgb( out var r, out var g, out var b, Hud.RgbYellowish );
		Hud.ScaleColors( ref r, ref g, ref b, a );

		var y = Hud.ScreenHeight - Hud.FontHeight - (Hud.FontHeight / 2);

		y += (int)(Hud.FontHeight * 0.2f);

		int x;

		if ( _weapon.AmmoType > 0 )
		{
			var iconWidth = _weapon.AmmoRect.Right - _weapon.AmmoRect.Left;

			if ( _weapon.Clip >= 0 )
			{
				x = Hud.ScreenWidth - (8 * ammoWidth) - iconWidth;
				x = Hud.DrawHudNumber( x, y, flags | Hud.Draw3Digits, _weapon.Clip, r, g, b );

				var barWidth = ammoWidth / 10;

				x += ammoWidth / 2;

				Hud.UnpackRgb( out r, out g, out b, Hud.RgbYellowish );

				Engine.FillRgba( x, y, barWidth, Hud.FontHeight, r, g, b, a );

				x += barWidth + (ammoWidth / 2);

				Hud.ScaleColors( ref r, ref g, ref b, a );

				x = Hud.DrawHudNumber( x, y, flags | Hud.Draw3Digits, Weapons.CountAmmo( _weapon.AmmoType ), r, g, b );
			}
			else
			{
				x = Hud.ScreenWidth - (4 * ammoWidth) - iconWidth;
				x = Hud.DrawHudNumber( x, y, flags | Hud.Draw3Digits, Weapons.CountAmmo( _weapon.AmmoType ), r, g, b );
			}

			var offset = (_weapon.AmmoRect.Bottom - _weapon.AmmoRect.Top) / 8;

			Engine.SpriteSet( _weapon.Ammo, r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y - offset, _weapon.AmmoRect );
		}

		if ( _weapon.Ammo2Type > 0 && Weapons.CountAmmo( _weapon.Ammo2Type ) > 0 )
		{
			var iconWidth = _weapon.Ammo2Rect.Right - _weapon.Ammo2Rect.Left;

			y -= Hud.FontHeight + (Hud.FontHeight / 4);
			x = Hud.ScreenWidth - (4 * ammoWidth) - iconWidth;
			x = Hud.DrawHudNumber( x, y, flags | Hud.Draw3Digits, Weapons.CountAmmo( _weapon.Ammo2Type ), r, g, b );

			var offset = (_weapon.Ammo2Rect.Bottom - _weapon.Ammo2Rect.Top) / 8;

			Engine.SpriteSet( _weapon.Ammo2, r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y - offset, _weapon.Ammo2Rect );
		}
	}

	private int DrawBar( int x, int y, int width, int height, float fraction )
	{
		fraction = Math.Clamp( fraction, 0f, 1f );

		int r, g, b;

		if ( fraction != 0f )
		{
			var filled = (int)(fraction * width);

			if ( filled <= 0 )
				filled = 1;

			Hud.UnpackRgb( out r, out g, out b, Hud.RgbGreenish );
			Engine.FillRgba( x, y, filled, height, r, g, b, 255 );

			x += filled;
			width -= filled;
		}

		Hud.UnpackRgb( out r, out g, out b, Hud.RgbYellowish );
		Engine.FillRgba( x, y, width, height, r, g, b, 128 );

		return x + width;
	}

	private void DrawAmmoBar( HudWeapon weapon, int x, int y, int width, int height )
	{
		if ( weapon is null || weapon.AmmoType == -1 )
			return;

		if ( Weapons.CountAmmo( weapon.AmmoType ) == 0 )
			return;

		x = DrawBar( x, y, width, height, (float)Weapons.CountAmmo( weapon.AmmoType ) / weapon.Max1 );

		if ( weapon.Ammo2Type == -1 )
			return;

		x += 5;

		DrawBar( x, y, width, height, (float)Weapons.CountAmmo( weapon.Ammo2Type ) / weapon.Max2 );
	}

	private void DrawWeaponList( float time )
	{
		if ( _activeSelection is null )
			return;

		var activeSlot = _activeSelection == HudWeapon.MenuOnly ? -1 : _activeSelection.Slot;
		var x = 10;
		var y = 10;

		if ( activeSlot > 0 && Weapons.GetFirstPos( activeSlot ) is null )
		{
			_activeSelection = HudWeapon.MenuOnly;
			activeSlot = -1;
		}

		int r, g, b;

		for ( var i = 0; i < WeaponsResource.MaxWeaponSlots; i++ )
		{
			Hud.UnpackRgb( out r, out g, out b, Hud.RgbYellowish );
			Hud.ScaleColors( ref r, ref g, ref b, 255 );

			Engine.SpriteSet( Hud.GetSprite( _bucket0 + i ), r, g, b );

			var width = _bucketWidth;

			if ( i == activeSlot && Weapons.GetFirstPos( activeSlot ) is { } first )
				width = first.ActiveRect.Right - first.ActiveRect.Left;

			Engine.SpriteDrawAdditive( 0, x, y, Hud.GetSpriteRect( _bucket0 + i ) );

			x += width + 5;
		}

		x = 10;

		for ( var i = 0; i < WeaponsResource.MaxWeaponSlots; i++ )
		{
			y = _bucketHeight + 10;

			if ( i == activeSlot )
			{
				var width = Weapons.GetFirstPos( i ) is { } first ? first.ActiveRect.Right - first.ActiveRect.Left : _bucketWidth;

				for ( var position = 0; position < WeaponsResource.MaxWeaponPositions; position++ )
				{
					var weapon = Weapons.GetWeaponSlot( i, position );

					if ( weapon is null || weapon.Id == 0 )
						continue;

					Hud.UnpackRgb( out r, out g, out b, Hud.RgbYellowish );

					if ( _activeSelection == weapon )
					{
						Engine.SpriteSet( weapon.Active, r, g, b );
						Engine.SpriteDrawAdditive( 0, x, y, weapon.ActiveRect );

						Engine.SpriteSet( Hud.GetSprite( _selection ), r, g, b );
						Engine.SpriteDrawAdditive( 0, x, y, Hud.GetSpriteRect( _selection ) );
					}
					else
					{
						if ( Weapons.HasAmmo( weapon ) )
						{
							Hud.ScaleColors( ref r, ref g, ref b, 192 );
						}
						else
						{
							Hud.UnpackRgb( out r, out g, out b, Hud.RgbRedish );
							Hud.ScaleColors( ref r, ref g, ref b, 128 );
						}

						Engine.SpriteSet( weapon.Inactive, r, g, b );
						Engine.SpriteDrawAdditive( 0, x, y, weapon.InactiveRect );
					}

					DrawAmmoBar( weapon, x + (_ammoBarWidth / 2), y, _ammoBarWidth, _ammoBarHeight );

					y += weapon.ActiveRect.Bottom - weapon.ActiveRect.Top + 5;
				}

				x += width + 5;
			}
			else
			{
				for ( var position = 0; position < WeaponsResource.MaxWeaponPositions; position++ )
				{
					var weapon = Weapons.GetWeaponSlot( i, position );

					if ( weapon is null || weapon.Id == 0 )
						continue;

					int a;

					if ( Weapons.HasAmmo( weapon ) )
					{
						Hud.UnpackRgb( out r, out g, out b, Hud.RgbYellowish );
						a = 128;
					}
					else
					{
						Hud.UnpackRgb( out r, out g, out b, Hud.RgbRedish );
						a = 96;
					}

					Engine.FillRgba( x, y, _bucketWidth, _bucketHeight, r, g, b, a );

					y += _bucketHeight + 5;
				}

				x += _bucketWidth + 5;
			}
		}
	}
}
