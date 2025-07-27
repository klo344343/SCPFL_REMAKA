using UnityEngine;

public class AchievementManager : MonoBehaviour
{
	public static void Achieve(string key)
	{
	}

	public static void StatsProgress(string key, string completeAchievement, int maxValue)
	{
		if (SteamManager.Running && !ServerStatic.IsDedicated)
		{
			int stat = SteamManager.GetStat(key);
			stat++;
			stat = Mathf.Clamp(stat, 0, maxValue);
			SteamManager.SetStat(key, stat);
			SteamManager.IndicateAchievementProgress(completeAchievement, (uint)stat, (uint)maxValue);
			Debug.Log("Stats Progress! " + key + " " + stat + "/" + maxValue);
		}
	}
}
