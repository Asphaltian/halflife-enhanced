using System;
using GoldSrc;

namespace HalfLife;

partial class Player
{
	public const float Autoaim2Degrees = 0.0348994967025f;
	public const float Autoaim5Degrees = 0.08715574274766f;
	public const float Autoaim8Degrees = 0.1391731009601f;
	public const float Autoaim10Degrees = 0.1736481776669f;

	private int _ammoAlias;

	public int AmmoSlot( int index )
	{
		if ( index >= 0 )
			return Ammo[index];

		return ClientSide ? _ammoAlias : LastItem is null ? 0 : 1;
	}

	public void SetAmmoSlot( int index, int value )
	{
		if ( index >= 0 )
			Ammo[index] = value;
		else if ( ClientSide )
			_ammoAlias = value;
	}

	public void SetLastItem( BasePlayerItem item )
	{
		LastItem = item;
		_ammoAlias = item is null ? 0 : 1;
	}

	public void TabulateAmmo()
	{
		Ammo9mm = AmmoInventory( GetAmmoIndex( "9mm" ) );
		Ammo357 = AmmoInventory( GetAmmoIndex( "357" ) );
		AmmoArGrenades = AmmoInventory( GetAmmoIndex( "ARgrenades" ) );
		AmmoBolts = AmmoInventory( GetAmmoIndex( "bolts" ) );
		AmmoBuckshot = AmmoInventory( GetAmmoIndex( "buckshot" ) );
		AmmoRockets = AmmoInventory( GetAmmoIndex( "rockets" ) );
		AmmoUranium = AmmoInventory( GetAmmoIndex( "uranium" ) );
		AmmoHornets = AmmoInventory( GetAmmoIndex( "Hornets" ) );
	}

	public int AmmoInventory( int ammoIndex ) => ammoIndex == -1 ? -1 : Ammo[ammoIndex];

	public static int GetAmmoIndex( string name )
	{
		if ( name is null )
			return -1;

		for ( var i = 1; i < BasePlayerItem.MaxAmmoSlots; i++ )
		{
			if ( BasePlayerItem.AmmoInfoArray[i].Name is { } ammoName && name.Equals( ammoName, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	public override int GiveAmmo( int count, string name, int max )
	{
		if ( name is null )
			return -1;

		if ( !HalfLifeDll.GameRules.CanHaveAmmo( this, name, max ) )
			return -1;

		var i = GetAmmoIndex( name );

		if ( i < 0 || i >= BasePlayerItem.MaxAmmoSlots )
			return -1;

		var add = Math.Min( count, max - Ammo[i] );

		if ( add < 1 )
			return i;

		Ammo[i] += add;

		if ( UserMessages.AmmoPickup != 0 )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.AmmoPickup, default, this );
			Server.WriteByte( GetAmmoIndex( name ) );
			Server.WriteByte( add );
			Server.MessageEnd();
		}

		TabulateAmmo();

		return i;
	}

	public void SendAmmoUpdate()
	{
		for ( var i = 0; i < BasePlayerItem.MaxAmmoSlots; i++ )
		{
			if ( Ammo[i] == _ammoLast[i] )
				continue;

			_ammoLast[i] = Ammo[i];

			Server.MessageBegin( MessageDest.One, UserMessages.AmmoX, default, this );
			Server.WriteByte( i );
			Server.WriteByte( Math.Max( Math.Min( Ammo[i], 254 ), 0 ) );
			Server.MessageEnd();
		}
	}

	public void ItemPreFrame()
	{
		if ( NextAttack > 0f )
			return;

		ActiveItem?.ItemPreFrame();
	}

	public void ItemPostFrame()
	{
		if ( Tank is not null )
			return;

		if ( NextAttack > 0f )
			return;

		ImpulseCommands();

		ActiveItem?.ItemPostFrame();
	}

	public bool AddPlayerItem( BasePlayerItem item )
	{
		for ( var insert = PlayerItems[item.ItemSlot]; insert is not null; insert = insert.Next )
		{
			if ( insert.ClassName != item.ClassName )
				continue;

			if ( item.AddDuplicate( insert ) )
			{
				HalfLifeDll.GameRules.PlayerGotWeapon( this, item );
				item.CheckRespawn();

				insert.UpdateItemInfo();
				ActiveItem?.UpdateItemInfo();

				item.Kill();
			}
			else if ( HalfLifeDll.EvilImpulse101 )
			{
				item.Kill();
			}

			return false;
		}

		if ( item.AddToPlayer( this ) )
		{
			HalfLifeDll.GameRules.PlayerGotWeapon( this, item );
			item.CheckRespawn();

			item.Next = PlayerItems[item.ItemSlot];
			PlayerItems[item.ItemSlot] = item;

			if ( HalfLifeDll.GameRules.ShouldSwitchWeapon( this, item ) )
				SwitchWeapon( item );

			return true;
		}

		if ( HalfLifeDll.EvilImpulse101 )
			item.Kill();

		return false;
	}

	public bool RemovePlayerItem( BasePlayerItem item )
	{
		if ( ActiveItem == item )
		{
			ResetAutoaim();

			item.Holster();
			item.NextThink = 0f;
			item.SetThink( null );

			ActiveItem = null;
			ViewModel = null;
			WeaponModel = null;
		}

		if ( LastItem == item )
			LastItem = null;

		var previous = PlayerItems[item.ItemSlot];

		if ( previous == item )
		{
			PlayerItems[item.ItemSlot] = item.Next;
			return true;
		}

		while ( previous is not null && previous.Next != item )
			previous = previous.Next;

		if ( previous is null )
			return false;

		previous.Next = item.Next;

		return true;
	}

	public void DropPlayerItem( string itemName )
	{
		if ( !HalfLifeDll.GameRules.IsMultiplayer() || Cvar.GetFloat( "mp_weaponstay" ) > 0f )
			return;

		if ( string.IsNullOrEmpty( itemName ) )
			itemName = null;

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			var weapon = PlayerItems[i];

			while ( weapon is not null )
			{
				if ( itemName is not null ? itemName == weapon.ClassName : weapon == ActiveItem )
					break;

				weapon = weapon.Next;
			}

			if ( weapon is null )
				continue;

			if ( !HalfLifeDll.GameRules.GetNextBestWeapon( this, weapon ) )
				return;

			Server.Globals.MakeVectors( Angles );

			Weapons &= ~(1 << weapon.Id);

			var weaponBox = (WeaponBox)Create( "weaponbox", Origin + (Server.Globals.Forward * 10f), Angles, this );

			weaponBox.Angles = new Vector3( 0f, weaponBox.Angles.y, 0f );
			weaponBox.PackWeapon( weapon );
			weaponBox.Velocity = (Server.Globals.Forward * 300f) + (Server.Globals.Forward * 100f);

			var ammoIndex = GetAmmoIndex( weapon.Ammo1 );

			if ( ammoIndex != -1 )
			{
				if ( (weapon.Flags & ItemFlags.Exhaustible) == ItemFlags.Exhaustible )
				{
					weaponBox.PackAmmo( weapon.Ammo1, Ammo[ammoIndex] );
					Ammo[ammoIndex] = 0;
				}
				else
				{
					weaponBox.PackAmmo( weapon.Ammo1, Ammo[ammoIndex] / 2 );
					Ammo[ammoIndex] /= 2;
				}
			}

			return;
		}
	}

	public bool HasPlayerItem( BasePlayerItem checkItem )
	{
		for ( var item = PlayerItems[checkItem.ItemSlot]; item is not null; item = item.Next )
		{
			if ( item.ClassName == checkItem.ClassName )
				return true;
		}

		return false;
	}

	public bool HasNamedPlayerItem( string itemName )
	{
		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var item = PlayerItems[i]; item is not null; item = item.Next )
			{
				if ( item.ClassName == itemName )
					return true;
			}
		}

		return false;
	}

	public bool HasPlayerItemFromId( int id )
	{
		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var item = PlayerItems[i]; item is not null; item = item.Next )
			{
				if ( item.Id == id )
					return true;
			}
		}

		return false;
	}

	public bool HasWeapons()
	{
		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			if ( PlayerItems[i] is not null )
				return true;
		}

		return false;
	}

	public bool SwitchWeapon( BasePlayerItem weapon )
	{
		if ( !weapon.CanDeploy() )
			return false;

		ResetAutoaim();

		ActiveItem?.Holster();

		ActiveItem = weapon;
		weapon.Deploy();

		return true;
	}

	public void SelectNextItem( int itemSlot )
	{
		var item = PlayerItems[itemSlot];

		if ( item is null )
			return;

		if ( item == ActiveItem )
		{
			item = ActiveItem.Next;

			if ( item is null )
				return;

			var last = item;

			while ( last.Next is not null )
				last = last.Next;

			last.Next = ActiveItem;
			ActiveItem.Next = null;
			PlayerItems[itemSlot] = item;
		}

		ResetAutoaim();

		ActiveItem?.Holster();

		ActiveItem = item;

		if ( ActiveItem is not null )
		{
			ActiveItem.Deploy();
			ActiveItem.UpdateItemInfo();
		}
	}

	public void SelectItem( string name )
	{
		if ( name is null )
			return;

		BasePlayerItem item = null;

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			if ( PlayerItems[i] is not null )
			{
				item = PlayerItems[i];

				while ( item is not null )
				{
					if ( item.ClassName == name )
						break;

					item = item.Next;
				}
			}

			if ( item is not null )
				break;
		}

		if ( item is null )
			return;

		if ( item == ActiveItem )
			return;

		ResetAutoaim();

		ActiveItem?.Holster();

		LastItem = ActiveItem;
		ActiveItem = item;

		if ( ActiveItem is not null )
		{
			ActiveItem.Deploy();
			ActiveItem.UpdateItemInfo();
		}
	}

	public void SelectLastItem()
	{
		if ( LastItem is null )
			return;

		if ( ActiveItem is not null && !ActiveItem.CanHolster() )
			return;

		ResetAutoaim();

		ActiveItem?.Holster();

		(ActiveItem, LastItem) = (LastItem, ActiveItem);

		ActiveItem.Deploy();
		ActiveItem.UpdateItemInfo();
	}

	public void GiveNamedItem( string name )
	{
		var entity = Server.CreateNamedEntity( name );

		if ( Util.IsNullEnt( entity ) )
		{
			Util.Alert( AlertType.Console, "NULL Ent in GiveNamedItem!" );
			return;
		}

		entity.Origin = Origin;
		entity.SpawnFlags |= BasePlayerItem.SpawnFlagNoRespawn;

		entity.DispatchSpawn();
		entity.DispatchTouch( this );
	}

	public void RemoveAllItems( bool removeSuit )
	{
		if ( ActiveItem is not null )
		{
			ResetAutoaim();
			ActiveItem.Holster();
			ActiveItem = null;
		}

		LastItem = null;

		if ( Tank is { } tank )
		{
			tank.Use( this, this, UseType.Off, 0f );
			Tank = null;
		}

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			ActiveItem = PlayerItems[i];

			while ( ActiveItem is not null )
			{
				var pending = ActiveItem.Next;

				ActiveItem.Drop();
				ActiveItem = pending;
			}

			PlayerItems[i] = null;
		}

		ActiveItem = null;

		ViewModel = null;
		WeaponModel = null;

		if ( removeSuit )
			Weapons = 0;
		else
			Weapons &= ~WeaponAllWeapons;

		Array.Clear( Ammo );

		UpdateClientData();

		Server.MessageBegin( MessageDest.One, UserMessages.CurWeapon, default, this );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.MessageEnd();
	}

	public void PackDeadPlayerItems()
	{
		var packWeapons = new BasePlayerWeapon[20];
		var packAmmo = new int[BasePlayerItem.MaxAmmoSlots + 1];
		var weaponCount = 0;
		var ammoCount = 0;

		Array.Fill( packAmmo, -1 );

		var weaponRules = HalfLifeDll.GameRules.DeadPlayerWeapons( this );
		var ammoRules = HalfLifeDll.GameRules.DeadPlayerAmmo( this );

		if ( weaponRules == RuleCode.PlayerDropGunNo && ammoRules == RuleCode.PlayerDropAmmoNo )
		{
			RemoveAllItems( true );
			return;
		}

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var item = PlayerItems[i]; item is not null; item = item.Next )
			{
				switch ( weaponRules )
				{
					case RuleCode.PlayerDropGunActive:
						if ( ActiveItem is not null && item == ActiveItem )
						{
							var weapon = (BasePlayerWeapon)item;

							packWeapons[weaponCount++] = weapon;

							if ( weapon.PrimaryAmmoType >= 0 )
							{
								var count = Math.Min( weapon.MaxClip - weapon.Clip, Ammo[weapon.PrimaryAmmoType] );

								weapon.Clip += count;
								Ammo[weapon.PrimaryAmmoType] -= count;
							}

							TabulateAmmo();
						}

						break;

					case RuleCode.PlayerDropGunAll:
						packWeapons[weaponCount++] = (BasePlayerWeapon)item;
						break;
				}
			}
		}

		if ( ammoRules != RuleCode.PlayerDropAmmoNo )
		{
			for ( var i = 0; i < BasePlayerItem.MaxAmmoSlots; i++ )
			{
				if ( Ammo[i] <= 0 )
					continue;

				switch ( ammoRules )
				{
					case RuleCode.PlayerDropAmmoAll:
						packAmmo[ammoCount++] = i;
						break;

					case RuleCode.PlayerDropAmmoActive:
						if ( ActiveItem is not null && (i == ActiveItem.PrimaryAmmoIndex || i == ActiveItem.SecondaryAmmoIndex) )
							packAmmo[ammoCount++] = i;

						break;
				}
			}
		}

		var weaponBox = (WeaponBox)Create( "weaponbox", Origin, Angles, this );

		weaponBox.Angles = new Vector3( 0f, weaponBox.Angles.y, 0f );

		weaponBox.SetThink( weaponBox.Kill );
		weaponBox.NextThink = Server.Globals.Time + 120f;

		if ( MultiplayBusters.IsBustingGame() )
		{
			if ( HasNamedPlayerItem( "weapon_egon" ) )
			{
				for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
				{
					if ( PlayerItems[i] is not { ClassName: "weapon_egon" } item )
						continue;

					weaponBox.PackWeapon( item );

					Server.SetModel( weaponBox, "models/w_egon.mdl" );

					weaponBox.Velocity = Vector3.Zero;
					weaponBox.RenderFx = RenderFx.GlowShell;
					weaponBox.RenderAmt = 25f;
					weaponBox.RenderColor = new Vector3( 0f, 75f, 250f );

					break;
				}
			}
		}
		else
		{
			var packItems = true;

			if ( ammoRules == RuleCode.PlayerDropAmmoActive && weaponRules == RuleCode.PlayerDropGunActive )
			{
				if ( packWeapons[0] is null || (packWeapons[0].ClassName == "weapon_satchel" && (packAmmo[0] == -1 || Ammo[packAmmo[0]] == 0)) )
					packItems = false;
			}

			if ( packItems )
			{
				for ( var i = 0; packAmmo[i] != -1; i++ )
					weaponBox.PackAmmo( BasePlayerItem.AmmoInfoArray[packAmmo[i]].Name, Ammo[packAmmo[i]] );

				for ( var i = 0; packWeapons[i] is not null; i++ )
					weaponBox.PackWeapon( packWeapons[i] );
			}

			weaponBox.Velocity = Velocity * 1.2f;
		}

		RemoveAllItems( true );
	}

	public void ResetAutoaim()
	{
		if ( AutoAim.x != 0f || AutoAim.y != 0f )
		{
			AutoAim = Vector3.Zero;
			Server.CrosshairAngle( this, 0f, 0f );
		}

		OnTarget = false;
	}

	public Vector3 GetAutoaimVector( float delta )
	{
		if ( ClientSide )
			return Vector3.Zero;

		if ( HalfLifeDll.SkillLevel == SkillData.Hard )
		{
			Server.Globals.MakeVectors( ViewAngle + PunchAngle );
			return Server.Globals.Forward;
		}

		var source = GetGunPosition();

		AutoAim = Vector3.Zero;

		var oldTargeting = OnTarget;
		var angles = AutoaimDeflection( source, 8192f, delta );

		if ( !HalfLifeDll.GameRules.AllowAutoTargetCrosshair() )
			OnTarget = false;
		else if ( oldTargeting != OnTarget )
			ActiveItem.UpdateItemInfo();

		if ( angles.x > 180f )
			angles.x -= 360f;

		if ( angles.x < -180f )
			angles.x += 360f;

		if ( angles.y > 180f )
			angles.y -= 360f;

		if ( angles.y < -180f )
			angles.y += 360f;

		if ( angles.x > 25f )
			angles.x = 25f;

		if ( angles.x < -25f )
			angles.x = -25f;

		if ( angles.y > 12f )
			angles.y = 12f;

		if ( angles.y < -12f )
			angles.y = -12f;

		if ( HalfLifeDll.SkillLevel == SkillData.Easy )
			AutoAim = (AutoAim * 0.67f) + (angles * 0.33f);
		else
			AutoAim = angles * 0.9f;

		if ( Cvar.GetFloat( "sv_aim" ) != 0f && Cvar.GetFloat( "sv_allow_autoaim" ) != 0f )
		{
			if ( AutoAim.x != _lastX || AutoAim.y != _lastY )
			{
				Server.CrosshairAngle( this, -AutoAim.x, AutoAim.y );

				_lastX = (int)AutoAim.x;
				_lastY = (int)AutoAim.y;
			}
		}

		Server.Globals.MakeVectors( ViewAngle + PunchAngle + AutoAim );

		return Server.Globals.Forward;
	}

	public Vector3 AutoaimDeflection( Vector3 source, float distance, float delta )
	{
		if ( Cvar.GetFloat( "sv_aim" ) == 0f || Cvar.GetFloat( "sv_allow_autoaim" ) == 0f )
		{
			OnTarget = false;
			return Vector3.Zero;
		}

		Server.Globals.MakeVectors( ViewAngle + PunchAngle + AutoAim );

		var bestDirection = Server.Globals.Forward;
		var bestDot = delta;
		Edict bestEntity = null;

		OnTarget = false;

		var trace = Util.TraceLine( source, source + (bestDirection * distance), DontIgnoreMonsters, this );

		if ( trace.Ent is not null && trace.Ent.DamageMode != DamageNo
			&& !((WaterLevel != 3 && trace.Ent.WaterLevel == 3) || (WaterLevel == 3 && trace.Ent.WaterLevel == 0)) )
		{
			if ( trace.Ent.DamageMode == DamageAim )
				OnTarget = true;

			return AutoAim;
		}

		for ( var i = 1; i < Server.NumEdicts; i++ )
		{
			if ( Server.GetEdict( i ) is not BaseEntity entity )
				continue;

			if ( entity.DamageMode != DamageAim )
				continue;

			if ( entity == this )
				continue;

			if ( !HalfLifeDll.GameRules.ShouldAutoAim( this, entity ) )
				continue;

			if ( !entity.IsAlive )
				continue;

			if ( (WaterLevel != 3 && entity.WaterLevel == 3) || (WaterLevel == 3 && entity.WaterLevel == 0) )
				continue;

			var center = entity.BodyTarget( source );
			var direction = Util.Normalize( center - source );

			if ( Vector3.Dot( direction, Server.Globals.Forward ) < 0f )
				continue;

			var dot = (float)(Math.Abs( Vector3.Dot( direction, Server.Globals.Right ) ) + (Math.Abs( Vector3.Dot( direction, Server.Globals.Up ) ) * 0.5));

			dot = (float)(dot * (1.0 + (0.2 * (MathLib.Length( center - source ) / distance))));

			if ( dot > bestDot )
				continue;

			trace = Util.TraceLine( source, center, DontIgnoreMonsters, this );

			if ( trace.Fraction != 1f && trace.Ent != entity )
				continue;

			if ( Relationship( entity ) < 0 && !entity.IsPlayer && !HalfLifeDll.GameRules.IsDeathmatch() )
				continue;

			bestDot = dot;
			bestEntity = entity;
			bestDirection = direction;
		}

		if ( bestEntity is null )
			return Vector3.Zero;

		bestDirection = MathLib.VectorAngles( bestDirection );
		bestDirection.x = -bestDirection.x;
		bestDirection = bestDirection - ViewAngle - PunchAngle;

		if ( bestEntity.DamageMode == DamageAim )
			OnTarget = true;

		return bestDirection;
	}
}
