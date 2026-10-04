using System;
using GoldSrc;

namespace HalfLife;

public enum HistorySlot
{
	Empty,
	Ammo,
	Weapon,
	Item
}

public sealed class HistoryResource( Hud hud )
{
	private const int MaxHistory = 12;

	private struct HistoryItem
	{
		public HistorySlot Type;
		public float DisplayTime;
		public int Count;
		public int Id;
	}

	private readonly HistoryItem[] _history = new HistoryItem[MaxHistory];

	private int _drawTime = 5;
	private int _currentSlot;

	public int Gap { get; set; }

	private int PickupGap => Gap + 5;

	private int PickupPickHeight => 32 + (Gap * 2);

	private int PickupHeightMax => hud.ScreenHeight - 100;

	public void Reset() => Array.Clear( _history );

	private ref HistoryItem NextSlot()
	{
		if ( (PickupGap * _currentSlot) + PickupPickHeight > PickupHeightMax || _currentSlot >= MaxHistory )
			_currentSlot = 0;

		return ref _history[_currentSlot++];
	}

	public void AddToHistory( HistorySlot type, int id, int count = 0 )
	{
		if ( type == HistorySlot.Ammo && count == 0 )
			return;

		ref var slot = ref NextSlot();

		_drawTime = (int)Cvar.GetFloat( "hud_drawhistory_time" );

		slot.Type = type;
		slot.Id = id;
		slot.Count = count;
		slot.DisplayTime = hud.Time + _drawTime;
	}

	public void AddToHistory( HistorySlot type, string name, int count = 0 )
	{
		if ( type != HistorySlot.Item )
			return;

		ref var slot = ref NextSlot();

		var index = hud.GetSpriteIndex( name );

		if ( index == -1 )
			return;

		slot.Id = index;
		slot.Type = type;
		slot.Count = count;

		_drawTime = (int)Cvar.GetFloat( "hud_drawhistory_time" );

		slot.DisplayTime = hud.Time + _drawTime;
	}

	private void CheckClearHistory()
	{
		foreach ( var item in _history )
		{
			if ( item.Type != HistorySlot.Empty )
				return;
		}

		_currentSlot = 0;
	}

	public void DrawAmmoHistory( float time )
	{
		var engine = hud.Engine;
		var weapons = hud.Ammo.Weapons;

		for ( var i = 0; i < MaxHistory; i++ )
		{
			ref var item = ref _history[i];

			if ( item.Type == HistorySlot.Empty )
				continue;

			item.DisplayTime = Math.Min( item.DisplayTime, hud.Time + _drawTime );

			if ( item.DisplayTime <= time )
			{
				item = default;
				CheckClearHistory();
				continue;
			}

			var scale = (item.DisplayTime - time) * 80;
			var y = hud.ScreenHeight - (PickupPickHeight + (PickupGap * i));

			Hud.UnpackRgb( out var r, out var g, out var b, Hud.RgbYellowish );

			if ( item.Type == HistorySlot.Ammo )
			{
				var found = weapons.GetAmmoPicFromWeapon( item.Id, out var sprite, out var rect );

				Hud.ScaleColors( ref r, ref g, ref b, Math.Min( (int)scale, 255 ) );

				var x = hud.ScreenWidth - (rect.Right - rect.Left) - 4;

				if ( found && sprite != 0 )
				{
					engine.SpriteSet( sprite, r, g, b );
					engine.SpriteDrawAdditive( 0, x, y, rect );
				}

				hud.DrawHudNumberString( x - 10, y, x - 100, item.Count, r, g, b );
			}
			else if ( item.Type == HistorySlot.Weapon )
			{
				var weapon = weapons.GetWeapon( item.Id );

				if ( !weapons.HasAmmo( weapon ) )
					Hud.UnpackRgb( out r, out g, out b, Hud.RgbRedish );

				Hud.ScaleColors( ref r, ref g, ref b, Math.Min( (int)scale, 255 ) );

				var x = hud.ScreenWidth - (weapon.InactiveRect.Right - weapon.InactiveRect.Left);

				engine.SpriteSet( weapon.Inactive, r, g, b );
				engine.SpriteDrawAdditive( 0, x, y, weapon.InactiveRect );
			}
			else if ( item.Type == HistorySlot.Item )
			{
				if ( item.Id == 0 )
					continue;

				var rect = hud.GetSpriteRect( item.Id );

				Hud.ScaleColors( ref r, ref g, ref b, Math.Min( (int)scale, 255 ) );

				var x = hud.ScreenWidth - (rect.Right - rect.Left) - 10;

				engine.SpriteSet( hud.GetSprite( item.Id ), r, g, b );
				engine.SpriteDrawAdditive( 0, x, y, rect );
			}
		}
	}
}
