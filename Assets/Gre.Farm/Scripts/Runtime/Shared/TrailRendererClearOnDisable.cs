using UnityEngine;

namespace Gre.Farm {
	/// <summary>
	/// プール返却（無効化）時に指定の TrailRenderer をクリアし、再利用時に前回の軌跡が残らないようにする。
	/// VfxManager は TrailRenderer を再生/停止管理しないため、プール再利用で前回の終点と今回の始点を
	/// つなぐ不正なトレイルが出るのを防ぐ。
	/// </summary>
	[DisallowMultipleComponent]
	public class TrailRendererClearOnDisable : MonoBehaviour {
		#region Serialize Fields

		[SerializeField] private TrailRenderer[] _trailRenderers;

		#endregion

		#region Unity Event Functions

		private void OnDisable() {
			// プール返却時に軌跡をクリアし、次回取得時に前回の軌跡が残らないようにする
			if (_trailRenderers == null) {
				return;
			}

			foreach (var trailRenderer in _trailRenderers) {
				if (trailRenderer != null) {
					trailRenderer.Clear();
				}
			}
		}

		#endregion
	}
}