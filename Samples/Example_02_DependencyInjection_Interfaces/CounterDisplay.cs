using UnityEngine;
using UnityEngine.UI;

namespace BeatThat.DependencyInjection.Example_DepenendencyInjection_Interfaces
{
    [RequireComponent(typeof(Text))]
    public class CounterDisplay : MonoBehaviour
    {
        public Text m_text;

        // A service can be injected by any registered interface
        [Inject] ICounter counter;

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