using System;

namespace GoldSrc;

partial class Client
{
	private const int PlayerLightKey = 4;
	private const float PlayerLightDuration = 0.001f;

	[ConVar( "r_bmodelinterp" )]
	private static float BrushModelInterpolation { get; set; } = 1f;

	[ConVar( "chase_active" )]
	public static float ChaseActive { get; private set; }

	[ConVar( "chase_back" )]
	private static float ChaseBack { get; set; } = 100f;

	[ConVar( "chase_up" )]
	private static float ChaseUp { get; set; } = 16f;

	[ConVar( "chase_right" )]
	private static float ChaseRight { get; set; }

	private sealed class PlayerModelState
	{
		public string Name = "";
		public Model Model;
	}

	private readonly PlayerModelState[] _playerModels = new PlayerModelState[Server.MaxClientsLimit];

	public int FrameCount => _frameCount;

	private bool IsSinglePlayerGame => (Server.Get( Scene ) is { Active: true } server ? server.MaxClients : _maxClients) == 1;

	public double OldClientTime => _oldTime;

	private void ChaseUpdate( ref RefParams refParams )
	{
		MathLib.AngleVectors( refParams.ClientViewAngles, out var forward, out var right, out _ );

		var origin = refParams.ViewOrigin;
		var destination = new Vector3(
			origin.x - (forward.x * ChaseBack) - (right.x * ChaseRight),
			origin.y - (forward.y * ChaseBack) - (right.y * ChaseRight),
			origin.z + ChaseUp );

		var trace = default( Trace );

		WorldModel.Hulls[0].ServerRecursiveCheck( 0, 0f, 1f, origin, origin + (4096f * forward), ref trace );

		var stop = trace.EndPos;
		var distance = Vector3.Dot( stop - origin, forward );

		if ( distance < 1f )
			distance = 1f;

		refParams.ViewAngles.x = (float)(-Math.Atan( (stop.z - origin.z) / distance ) / Math.PI * 180.0);
		refParams.ViewOrigin = destination;
	}

	private void LinkPlayers()
	{
		foreach ( var visible in _visibleEntities )
			visible.Visible = false;

		_visibleEntities.Clear();

		var frame = CurrentFrame;

		for ( var j = 0; j < Server.MaxClientsLimit; j++ )
		{
			ref readonly var state = ref frame.PlayerState[j];

			if ( state.MessageNum != _parseCount )
				continue;

			if ( (!_clientDll.IsThirdPerson() && j == _viewEntity - 1 && ChaseActive == 0f) || state.ModelIndex == 0 || (state.Effects & (int)Effects.NoDraw) != 0 )
				continue;

			if ( _visibleEntities.Count >= MaxVisibleEntities )
				break;

			var entity = CreateEntity( j + 1 );

			entity.Index = j + 1;

			if ( j == _playerNum )
				entity.SetLocalPlayerOrigin( state.Origin );

			entity.LinkPlayer( _messageTime );

			if ( j == _playerNum )
			{
				entity.Origin = _simOrigin;
			}
			else
			{
				entity.Origin = entity.CurState.Origin;
				entity.Angles = entity.CurState.Angles;
				entity.ComputePlayerOrigin( (float)(ClientTime - ExInterp) );
			}

			if ( j != _viewEntity - 1 )
			{
				var effects = (Effects)entity.CurState.Effects;

				if ( (effects & Effects.BrightLight) == Effects.BrightLight )
				{
					var light = AllocDlight( PlayerLightKey );

					light.Origin = entity.Origin + new Vector3( 0f, 0f, 16f );
					light.R = 250;
					light.G = 250;
					light.B = 250;
					light.Radius = Server.RandomFloat( 400f, 431f );
					light.Die = (float)(ClientTime + PlayerLightDuration);
				}

				if ( (effects & Effects.DimLight) == Effects.DimLight )
				{
					var light = AllocDlight( PlayerLightKey );

					light.Origin = entity.Origin;
					light.R = 100;
					light.G = 100;
					light.B = 100;
					light.Radius = Server.RandomFloat( 200f, 231f );
					light.Die = (float)(ClientTime + PlayerLightDuration);
				}
			}

			entity.SetAttachments( entity.Origin );
			entity.Visible = true;

			_visibleEntities.Add( entity );
		}
	}

	public Model SetupPlayerModel( int index, Model entityModel )
	{
		var state = _playerModels[index] ??= new PlayerModelState();
		var model = GetPlayerInfo( index ).Model;

		if ( (Host.Developer == 0f && IsSinglePlayerGame) || model.Length == 0 )
		{
			state.Name = "";
			state.Model = entityModel;

			return state.Model;
		}

		if ( state.Name == model )
			return state.Model;

		state.Name = model;
		state.Model = ModelCache.ForName( $"models/player/{model}/{model}.mdl" ) ?? entityModel;

		return state.Model;
	}

	private void LinkPlayerLight( ClientEntity player )
	{
		var effects = player?.PacketEffects ?? default;

		var newFlashlight = NewFlashlight != 0f && (effects & Effects.BrightLight) == 0 && (effects & Effects.DimLight) == Effects.DimLight;

		if ( !newFlashlight )
			FlashlightOff();

		if ( newFlashlight || (effects & (Effects.BrightLight | Effects.DimLight)) == 0 )
		{
			if ( _playerLight is { Key: 1 } )
				_playerLight.Die = (float)ClientTime;

			_playerLight = null;

			if ( newFlashlight )
				UpdateFlashlight( player.Origin + _viewHeight );

			return;
		}

		var light = _playerLight is { Key: 1 } ? _playerLight : AllocDlight( 1 );

		if ( (effects & Effects.BrightLight) == Effects.BrightLight )
		{
			light.R = 250;
			light.G = 250;
			light.B = 250;
			light.Radius = 400f;
			light.Origin = player.Origin + new Vector3( 0f, 0f, 16f );
		}
		else
		{
			MathLib.AngleVectors( _viewAngles, out var forward, out _, out _ );

			var start = player.Origin + _viewHeight;
			var end = start + (forward * 2000f);

			_pmove.UseHull = 2;

			var trace = _pmove.PlayerTrace( start, end, TraceFlags.StudioBox, -1 );

			light.Origin = trace.Ent > 0 && _pmove.PhysEnts[trace.Ent].StudioModel is not null ? _pmove.PhysEnts[trace.Ent].Origin : trace.EndPos;

			var falloff = trace.Fraction * 2000f;

			falloff = falloff >= 500f ? 500f / falloff : 1f;

			var brightness = (byte)(int)(falloff * falloff * 255f);

			light.Radius = 80f;
			light.R = brightness;
			light.G = brightness;
			light.B = brightness;
		}

		_playerLight = light;
		light.Die = (float)(ClientTime + 0.2);
	}
}
