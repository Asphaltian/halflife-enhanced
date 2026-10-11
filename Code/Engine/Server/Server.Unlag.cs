using System;

namespace GoldSrc;

partial class Server
{
	private const double MaxUnlagLatency = 1.5;
	private const float MaxUnlagInterpolation = 0.1f;
	private const double UnlagTeleportDistance = 128.0;
	private const double MaxUnlagFrameAge = 1.0;

	[ConVar( "sv_unlag" )]
	private static float SvUnlag { get; set; } = 1f;

	[ConVar( "sv_maxunlag" )]
	private static float SvMaxUnlag { get; set; } = 0.5f;

	[ConVar( "sv_unlagpush" )]
	private static float SvUnlagPush { get; set; }

	private struct TruePosition
	{
		public bool Active;
		public bool NeedRelink;
		public Vector3 NewOrigin;
		public Vector3 OldOrigin;
		public Vector3 InitialCorrectionOrigin;
		public Vector3 OldAbsMin;
		public Vector3 OldAbsMax;
		public bool DeadFlag;
		public Vector3 TempOrigin;
		public bool TempOriginSet;
	}

	private readonly TruePosition[] _truePositions = new TruePosition[MaxClientsLimit];

	private bool _noFind;

	private bool UseTruePosition( int slot )
	{
		var client = _hostClient;

		return client.LocalWeapons && client.LagCompensation && SvUnlag != 0f && MaxClients > 1 && client.Active
			&& slot >= 0 && slot < MaxClients && _truePositions[slot].Active && _truePositions[slot].NeedRelink;
	}

	private void SetupMove( ServerClient client )
	{
		Array.Clear( _truePositions );
		_noFind = true;

		if ( !GameDll.AllowLagCompensation() || SvUnlag == 0f || !client.LocalWeapons || !client.LagCompensation || MaxClients <= 1 || !client.Active )
			return;

		_noFind = false;

		for ( var i = 0; i < MaxClients; i++ )
		{
			var other = _clients[i];

			if ( other == client || !other.Active )
				continue;

			var edict = GetEdict( other.EdictIndex );

			ref var position = ref _truePositions[i];

			position.OldOrigin = edict.Origin;
			position.OldAbsMin = edict.AbsMin;
			position.OldAbsMax = edict.AbsMax;
			position.Active = true;
		}

		var latency = (float)Math.Min( client.Latency, MaxUnlagLatency );

		if ( SvMaxUnlag != 0f )
		{
			if ( SvMaxUnlag < 0f )
				SvMaxUnlag = 0f;

			if ( SvMaxUnlag <= latency )
				latency = SvMaxUnlag;
		}

		var interpolation = (float)Math.Max( client.NextMessageInterval, Math.Min( client.LastCmd.LerpMsec / 1000f, MaxUnlagInterpolation ) );
		var targetTime = (float)Math.Min( (float)(Host.RealTime - latency - interpolation + SvUnlagPush), Host.RealTime );

		ServerFrame nextFrame = null;

		for ( var i = 0; i < UpdateBackup; i++ )
		{
			var frame = client.Frames[(client.Netchan.OutgoingSequence - i - 1) & UpdateMask];
			var pack = frame.Entities;

			for ( var j = 0; j < pack.Count; j++ )
			{
				ref var state = ref pack.Entities[j];

				if ( state.Number <= 0 || state.Number > MaxClients )
					continue;

				ref var position = ref _truePositions[state.Number - 1];

				if ( position.DeadFlag )
					continue;

				if ( state.Health <= 0 )
					position.DeadFlag = true;

				if ( (((Effects)state.Effects) & Effects.NoInterp) == Effects.NoInterp )
					position.DeadFlag = true;

				if ( !position.TempOriginSet )
				{
					position.TempOriginSet = true;
				}
				else
				{
					for ( var k = 0; k < 3; k++ )
					{
						if ( Math.Abs( (double)(state.Origin[k] - position.TempOrigin[k]) ) > UnlagTeleportDistance )
						{
							position.DeadFlag = true;
							break;
						}
					}
				}

				position.TempOrigin = state.Origin;
			}

			if ( frame.SentTime >= targetTime )
			{
				nextFrame = frame;
				continue;
			}

			var elapsed = targetTime - frame.SentTime;

			if ( elapsed > MaxUnlagFrameAge )
				break;

			var fraction = 0f;

			if ( nextFrame is null )
			{
				nextFrame = frame;
			}
			else if ( (float)(nextFrame.SentTime - frame.SentTime) != 0f )
			{
				var value = Math.Min( (float)(elapsed / (float)(nextFrame.SentTime - frame.SentTime)), 1f );

				if ( value >= 0f )
					fraction = value;
			}

			var next = nextFrame.Entities;

			for ( var j = 0; j < pack.Count; j++ )
			{
				ref var state = ref pack.Entities[j];

				if ( state.Number <= 0 || state.Number > MaxClients )
					continue;

				var other = _clients[state.Number - 1];

				ref var position = ref _truePositions[state.Number - 1];

				if ( other == client || !other.Active || position.DeadFlag )
					continue;

				if ( !position.Active )
				{
					Log.Trace( $"tried to store off position of bogus player {j}/{other.Name}\n" );
					continue;
				}

				var origin = state.Origin;

				for ( var k = 0; k < next.Count; k++ )
				{
					if ( next.Entities[k].Number != state.Number )
						continue;

					origin = state.Origin + (fraction * (next.Entities[k].Origin - state.Origin));
					break;
				}

				position.NewOrigin = origin;
				position.InitialCorrectionOrigin = origin;

				var edict = GetEdict( other.EdictIndex );

				if ( !MathLib.VectorCompare( origin, edict.Origin ) )
				{
					edict.Origin = origin;
					LinkEdict( edict, false );

					position.NeedRelink = true;
				}
			}

			return;
		}

		Array.Clear( _truePositions );
		_noFind = true;
	}

	private void RestoreMove( ServerClient client )
	{
		if ( _noFind )
		{
			_noFind = false;
			return;
		}

		if ( !GameDll.AllowLagCompensation() || MaxClients <= 1 || SvUnlag == 0f || !client.LocalWeapons || !client.LagCompensation || !client.Active )
			return;

		for ( var i = 0; i < MaxClients; i++ )
		{
			var other = _clients[i];

			if ( other == client || !other.Active )
				continue;

			ref var position = ref _truePositions[i];

			if ( MathLib.VectorCompare( position.NewOrigin, position.OldOrigin ) || !position.NeedRelink )
				continue;

			if ( !position.Active )
			{
				Log.Trace( $"SV_RestoreMove:  Tried to restore 'inactive' player {i}/{other.Name}\n" );
				continue;
			}

			var edict = GetEdict( other.EdictIndex );

			if ( MathLib.VectorCompare( position.InitialCorrectionOrigin, edict.Origin ) )
			{
				edict.Origin = position.OldOrigin;
				LinkEdict( edict, false );
			}
		}
	}
}
