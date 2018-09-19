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
    [EditDefine(
        "DEPENDENCY_INJECTION_DISABLE_PREMAP_TYPE_INJECTIONS",
        "By default, dependency injection will initialize with a one time op to pre build a map of all types that have injections"
    )]
    public static class InjectDependencies
    {
        public static bool On(object instance)
        {
            var instType = instance.GetType();

            TypeInjections typeInjections = GetTypeInjections(instType);

            var eventHandler = instance as DependencyInjectionEventHandler;
            var willInjectEventSent = false;

            if (typeInjections.fields != null && typeInjections.fields.Length > 0)
            {
                foreach (var f in typeInjections.fields)
                {
                    if (f.GetValue(instance) != null)
                    { // don't overwrite if already set
                        continue;
                    }

                    if (!(Services.exists && Services.Get.hasInit))
                    {
                        InjectOnServicesInit(instance);
                        return false;
                    }

                    if (eventHandler != null && !willInjectEventSent)
                    {
                        eventHandler.OnWillInjectDependencies();
                        willInjectEventSent = true;
                    }

                    var v = Services.Get.GetService(f.FieldType);
                    if (v == null)
                    {
#if UNITY_EDITOR || DEBUG_UNSTRIP
                        Debug.LogError("[" + Time.frameCount + "] service not registered for type " + f.FieldType
                            + " marked for injection by type " + instType);
#endif
                        continue;
                    }

                    f.SetValue(instance, v);
                }
            }

            if (typeInjections.properties != null && typeInjections.properties.Length > 0)
            {
                foreach (var p in typeInjections.properties)
                {
                    if (p.GetValue(instance, null) != null)
                    { // don't overwrite if already set
                        continue;
                    }

                    if (!(Services.exists && Services.Get.hasInit))
                    {
                        InjectOnServicesInit(instance);
                        return false;
                    }

                    if (eventHandler != null && !willInjectEventSent)
                    {
                        eventHandler.OnWillInjectDependencies();
                        willInjectEventSent = true;
                    }

                    var v = Services.Get.GetService(p.PropertyType);
                    if (v == null)
                    {
#if UNITY_EDITOR || DEBUG_UNSTRIP
                        Debug.LogError("[" + Time.frameCount + "] service not registered for type " + p.PropertyType
                        + " marked for injection by type " + instType);
#endif
                        continue;
                    }


                    p.SetValue(instance, v, null);
                }
            }

            if (eventHandler != null)
            {
                eventHandler.OnDidInjectDependencies();
            }

            return true;
        }

        private static IDictionary<Type, TypeInjections> GetTypeInjectionsByType()
        {
            if (m_typeInjectionsByType == null)
            {
                var typeInjectionsByType = new Dictionary<Type, TypeInjections>();

#if !DEPENDENCY_INJECTION_DISABLE_PREMAP_TYPE_INJECTIONS
                TypeInjections cur;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type t in a.GetTypes())
                    {
                        if (FindTypeInjections(t, out cur))
                        {
                            typeInjectionsByType[t] = cur;
                        }
                    }
                }
#endif

                m_typeInjectionsByType = typeInjectionsByType;
            }
            return m_typeInjectionsByType;
        }

        private static FieldInfo[] EMPTY_FIELDS = new FieldInfo[0];
        private static PropertyInfo[] EMPTY_PROPS = new PropertyInfo[0];

        private static bool FindTypeInjections(Type t, out TypeInjections result)
        {
            result = new TypeInjections();

            using (var fields = ListPool<FieldInfo>.Get())
            using (var injectFields = ListPool<FieldInfo>.Get())
            {

                t.GetFieldsIncludingBaseTypes(fields,
                                            BindingFlags.Instance
                                            | BindingFlags.Public
                                            | BindingFlags.NonPublic);

                foreach (var f in fields)
                {
                    var fAttrs = f.GetCustomAttributes(true);
                    foreach (var a in fAttrs)
                    {
                        if (!typeof(InjectAttribute).IsAssignableFrom(a.GetType()))
                        {
                            continue;
                        }
                        injectFields.Add(f);
                    }
                }
                result.fields = injectFields.Count > 0 ? injectFields.ToArray() : EMPTY_FIELDS;
            }


            using (var props = ListPool<PropertyInfo>.Get())
            using (var injectProps = ListPool<PropertyInfo>.Get())
            {
                t.GetPropertiesIncludingBaseTypes(props, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var p in props)
                {
                    var pAttrs = p.GetCustomAttributes(true);
                    foreach (var a in pAttrs)
                    {
                        if (!typeof(InjectAttribute).IsAssignableFrom(a.GetType()))
                        {
                            continue;
                        }
                        injectProps.Add(p);
                    }
                }
                result.properties = injectProps.Count > 0 ? injectProps.ToArray() : EMPTY_PROPS;
            }

            return result.fields.Length > 0 || result.properties.Length > 0;
        }

        private static TypeInjections GetTypeInjections(Type t)
        {
            var typeInjectionsByType = GetTypeInjectionsByType();

            TypeInjections result;
            if (typeInjectionsByType.TryGetValue(t, out result))
            {
                return result;
            }

#if DEPENDENCY_INJECTION_DISABLE_PREMAP_TYPE_INJECTIONS
            // we haven't premapped all, so we have to find and store for this type
            FindTypeInjections(t, out result);
            m_typeInjectionsByType[t] = result;
            return result;
#else
            return new TypeInjections
            {
                fields = EMPTY_FIELDS,
                properties = EMPTY_PROPS
            };
#endif
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
        private static Dictionary<Type, TypeInjections> m_typeInjectionsByType;
	}

}



