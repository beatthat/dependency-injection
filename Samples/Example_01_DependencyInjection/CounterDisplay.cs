using UnityEngine;
using UnityEngine.UI;

namespace BeatThat.DependencyInjection.Example_DepenendencyInjection
{
    [RequireComponent(typeof(Text))]
    public class CounterDisplay : MonoBehaviour
    {
        public Text m_text;

        // Add the [Inject] attribute to properties 
        // to have them set with the service registered to the property's type
        [Inject] CounterService counter;

        private void Start()
        {
            
            //Something needs to call DependencyInjection.InjectDependencies.
            //
            //One option is to call it in MonoBehaviour::Start...
            InjectDependencies.On(this);

            m_text = GetComponent<Text>();

            // since dependency injection is complete, the counter property should be set now
            this.counter.onUpdated.AddListener(this.UpdateDisplay);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            m_text.text = this.counter.count.ToString();
        }


    }
}