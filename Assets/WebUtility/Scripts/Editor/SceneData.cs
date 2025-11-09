using UnityEngine;

namespace WebUtility
{
    public class SceneData : MonoBehaviour
    {
        [SerializeField] private string sceneValue = "Default Value";
        [SerializeField] private int sceneNumber = 1;
        [SerializeField] private bool isActive = true;

        public string Value => sceneValue;
        public int Number => sceneNumber;
        public bool IsActive => isActive;

        public void SetValue(string newValue) => sceneValue = newValue;
        public void SetNumber(int newNumber) => sceneNumber = newNumber;
        public void SetActive(bool active) => isActive = active;
    }
}