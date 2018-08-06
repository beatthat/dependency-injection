using System;
using System.Collections.Generic;
using System.Reflection;
using BeatThat.Defines;
using BeatThat.Pools;
using BeatThat.SafeRefs;
using BeatThat.Service;
using BeatThat.TypeExts;
using UnityEngine;

namespace BeatThat.DependencyInjection
{
    [EditDefine(
        "DEPENDENCY_INJECTION_DISABLE_AUTO_INIT_SERVICES",
        "By default, dependency injection will call Services.Init if it encounters an [Inject] tag and services are neither init nor init in progress. Define this symbol to disable that behaviour."
    )]
    public static class InjectDependencies 
	{
		public static bool On(object instance)
		{
			var instType = instance.GetType ();
	
			var typeInjections = GetTypeInjectionFields (instType);

            var eventHandler = instance as DependencyInjectionEventHandler;
            var willInjectEventSent = false;

			if (typeInjections.fields != null && typeInjections.fields.Length > 0) {
				foreach (var f in typeInjections.fields) {
					if (f.GetValue (instance) != null) { // don't overwrite if already set
						continue;
					}

					if (!(Services.exists && Services.Get.hasInit)) {
						InjectOnServicesInit (instance);
                        return false;
					}

                    if(eventHandler != null && !willInjectEventSent) {
                        eventHandler.OnWillInjectDependencies();
                        willInjectEventSent = true;
                    }

					var v = Services.Get.GetService (f.FieldType);
					if (v == null) {
						#if UNITY_EDITOR || DEBUG_UNSTRIP
						Debug.LogError("[" + Time.frameCount + "] service not registered for type " + f.FieldType 
							+ " marked for injection by type " + instType);
						#endif
						continue;
					}

					f.SetValue (instance, v);
				}
			}

			if (typeInjections.properties != null && typeInjections.properties.Length > 0) {
				foreach (var p in typeInjections.properties) {
					if (p.GetValue (instance, null) != null) { // don't overwrite if already set
						continue;
					}

					if (!(Services.exists && Services.Get.hasInit)) {
						InjectOnServicesInit (instance);
                        return false;
					}

                    if (eventHandler != null && !willInjectEventSent)
                    {
                        eventHandler.OnWillInjectDependencies();
                        willInjectEventSent = true;
                    }

					var v = Services.Get.GetService (p.PropertyType);
					if (v == null) {
						#if UNITY_EDITOR || DEBUG_UNSTRIP
                            Debug.LogError("[" + Time.frameCount + "] service not registered for type " + p.PropertyType 
							+ " marked for injection by type " + instType);
						#endif
						continue;
					}


					p.SetValue (instance, v, null);
				}
			}

            if(eventHandler != null) {
                eventHandler.OnDidInjectDependencies();
            }

            return true;
		}

		private static TypeInjections GetTypeInjectionFields(Type t)
		{
			TypeInjections result;
			if (m_typeInjectionFieldsByType.TryGetValue (t, out result)) {
				return result;
			}


            using (var fields = ListPool<FieldInfo>.Get())
			using (var injectFields = ListPool<FieldInfo>.Get ()) {
                
                t.GetFieldsIncludingBaseTypes(fields, 
                                            BindingFlags.Instance 
                                            | BindingFlags.Public 
                                            | BindingFlags.NonPublic);

				foreach (var f in fields) {
					var fAttrs = f.GetCustomAttributes (true);
					foreach (var a in fAttrs) {
						if (!typeof(InjectAttribute).IsAssignableFrom (a.GetType ())) {
							continue;
						}
						injectFields.Add (f);
					}
				}
				result.fields = injectFields.ToArray ();
			}


            using (var props = ListPool<PropertyInfo>.Get())
			using (var injectProps = ListPool<PropertyInfo>.Get ()) {
                t.GetPropertiesIncludingBaseTypes (props, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (var p in props) {
					var pAttrs = p.GetCustomAttributes (true);
					foreach (var a in pAttrs) {
						if (!typeof(InjectAttribute).IsAssignableFrom (a.GetType ())) {
							continue;
						}
						injectProps.Add (p);
					}
				}
				result.properties = injectProps.ToArray ();
			}

			m_typeInjectionFieldsByType [t] = result;

			return result;
		}

		struct TypeInjections
		{
			public FieldInfo[] fields;
			public PropertyInfo[] properties;
		}

        private static void InjectOnServicesInit(object inst)
        {
            var eventHandler = inst as DependencyInjectionEventHandler;
            if (eventHandler != null)
            {
                eventHandler.OnDependencyInjectionWaitingForServicesReady();
            }

            if (m_injectOnServicesInit == null)
            {
                m_injectOnServicesInit = ListPool<SafeRef<object>>.Get();

                Services.InitStatusUpdated.AddListener((s) =>
                {
                    if (!s.hasInit)
                    {
                        return;
                    }

                    foreach (var o in m_injectOnServicesInit)
                    {
                        if (o.value == null)
                        {
                            continue;
                        }
                        On(o.value);
                    }

                    m_injectOnServicesInit.Dispose();
                    m_injectOnServicesInit = null;
                });
            }

            m_injectOnServicesInit.Add(new SafeRef<object>(inst));

#if !DEPENDENCY_INJECTION_DISABLE_AUTO_INIT_SERVICES
            if (!Services.exists || !Services.Get.isInitInProgress)
            {
                Services.Init();
            }
#endif
        }

		private static ListPoolList<SafeRef<object>> m_injectOnServicesInit;
		private static Dictionary<Type, TypeInjections> m_typeInjectionFieldsByType = new Dictionary<Type, TypeInjections> ();
	}

}



