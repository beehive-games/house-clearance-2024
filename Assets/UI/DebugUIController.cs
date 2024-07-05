using UnityEngine.UIElements;

namespace UI
{
    public class DebugUIController : VisualElement
    {
        Label _healthLabel;
        Label _ammoLabel;
    
        // This function retrieves a reference to the 
        // character name label inside the UI element.
        public void SetVisualElement(VisualElement visualElement)
        {
            _healthLabel = visualElement.Q<Label>("healthValue");
            _ammoLabel = visualElement.Q<Label>("ammoValue");
        }
    
        public void SetHealthData(float health)
        {
            _healthLabel.text = health.ToString();
        }
    
        public void SetAmmoData(float ammo)
        {
            _ammoLabel.text = ammo.ToString();
        }
    }
}
