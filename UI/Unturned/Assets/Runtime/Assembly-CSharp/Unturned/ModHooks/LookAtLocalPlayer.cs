using UnityEngine;

namespace SDG.Unturned
{
	public class LookAtLocalPlayer : MonoBehaviour
	{
#if GAME
		private void LateUpdate()
		{
			if (Player.LocalPlayer != null)
			{
				transform.LookAt(Player.LocalPlayer.look.aim);
			}
		}
#endif // GAME
	}
}
