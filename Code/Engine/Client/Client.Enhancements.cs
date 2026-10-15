namespace GoldSrc;

partial class Client
{
	[ConVar( "r_ao", ConVarFlags.Saved )]
	public static float AmbientOcclusionEnabled { get; set; }

	[ConVar( "r_newmuzzleflash", ConVarFlags.Saved )]
	public static float NewMuzzleFlash { get; set; }

	private AmbientOcclusion _ambientOcclusion;

	private void UpdateEnhancements( CameraComponent camera )
	{
		if ( AmbientOcclusionEnabled != 0f || _ambientOcclusion.IsValid() )
		{
			if ( !_ambientOcclusion.IsValid() )
				_ambientOcclusion = camera.GameObject.AddComponent<AmbientOcclusion>();

			_ambientOcclusion.Enabled = AmbientOcclusionEnabled != 0f;
		}
	}
}
