using TMPro;
using UnityEngine;

namespace _Game.Scripts.UI
{
    public class ResourcesManager : MonoBehaviour
    {
        public static ResourcesManager Instance = null;

        [SerializeField] int wood = 0, stone = 0;
        public TMP_Text woodText;
        public TMP_Text stoneText;

        public void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void TakeResource(GettingResources gettingResources)
        {
            wood += gettingResources.Wood;
            stone += gettingResources.Stone;
            woodText.text = wood.ToString();
            stoneText.text = stone.ToString();
        }
    }
}