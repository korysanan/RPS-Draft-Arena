using System.Collections.Generic;
using UnityEngine;

public class PracticeCardController : MonoBehaviour
{
    [SerializeField] private List<GameObject> cards = new List<GameObject>();
    [SerializeField] private List<Texture> elementTextures = new List<Texture>();

    private void Start()
    {
        int count = ResolveCount(PracticeSettings.Rps);

        var pool = new List<Texture>(count);
        for (int i = 0; i < count && i < elementTextures.Count; i++)
            pool.Add(elementTextures[i]);
        Shuffle(pool);

        for (int i = 0; i < cards.Count; i++)
        {
            bool active = i < count;
            if (cards[i] != null)
                cards[i].SetActive(active);
            if (!active) continue;

            var flip = cards[i].GetComponent<CardFlip>();
            if (flip != null && i < pool.Count)
                flip.SetBackTexture(pool[i]);
        }
    }

    private static int ResolveCount(PracticeSetupManager.RPSType rps)
    {
        return rps switch
        {
            PracticeSetupManager.RPSType.RPS3 => 3,
            PracticeSetupManager.RPSType.RPS5 => 5,
            PracticeSetupManager.RPSType.RPS7 => 7,
            _ => 7
        };
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
