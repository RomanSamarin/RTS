using TMPro;
using UnityEngine;

namespace _Game.Scripts.UI
{
    public class ResourcesManager : MonoBehaviour
    {
        public static ResourcesManager Instance = null;

        [SerializeField] private int wood = 0;
        [SerializeField] private int stone = 0;
        [SerializeField] private int wheat = 0;

        public TMP_Text woodText;
        public TMP_Text stoneText;
        public TMP_Text wheatText;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            UpdateUI();
        }

        public void TakeResource(GettingResources res)
        {
            wood += res.Wood;
            stone += res.Stone;
            wheat += res.Wheat;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (woodText != null) woodText.text = wood.ToString();
            if (stoneText != null) stoneText.text = stone.ToString();
            if (wheatText != null) wheatText.text = wheat.ToString();
        }

        // Проверка ресурсов для казармы
        public bool HasEnough(int w, int s, int wh)
        {
            return wood >= w && stone >= s && wheat >= wh;
        }

        // Потратить ресурсы
        public void Spend(int w, int s, int wh)
        {
            wood -= w;
            stone -= s;
            wheat -= wh;
            UpdateUI();
        }
    }
}