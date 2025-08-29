using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public class DiceAudioTools : EditorWindow
{
    private AudioMixerGroup sfxGroup;
    private bool includeChildren = true;

    [MenuItem("Tools/Dice/Audio Setup...")]
    public static void Open()
    {
        GetWindow<DiceAudioTools>(false, "Dice Audio Setup", true);
    }

    private void OnGUI()
    {
        GUILayout.Label("Assign AudioMixerGroup (SFX) to Dice", EditorStyles.boldLabel);
        sfxGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("SFX Mixer Group", sfxGroup, typeof(AudioMixerGroup), false);
        includeChildren = EditorGUILayout.ToggleLeft("Also assign to per-die AudioSources (children)", includeChildren);

        EditorGUILayout.Space();
        if (GUILayout.Button("Assign To Scene"))
        {
            AssignToScene(sfxGroup, includeChildren);
        }

        EditorGUILayout.HelpBox(
            "Usage:\n1) Create an AudioMixer (Create > Audio Mixer), add a group named 'SFX'.\n2) Drag the 'SFX' group here.\n3) Click 'Assign To Scene' to route DiceManager + die AudioSources to this group.",
            MessageType.Info);
    }

    private void AssignToScene(AudioMixerGroup group, bool assignChildren)
    {
        if (group == null)
        {
            Debug.LogWarning("DiceAudioTools: No AudioMixerGroup assigned.");
            return;
        }

        int count = 0;
        var managers = Object.FindObjectsOfType<DiceManager>();
        foreach (var m in managers)
        {
            Undo.RecordObject(m, "Assign DiceManager.sfxMixer");
            m.sfxMixer = group;
            EditorUtility.SetDirty(m);

            if (assignChildren)
            {
                var sources = m.GetComponentsInChildren<AudioSource>(true);
                foreach (var src in sources)
                {
                    Undo.RecordObject(src, "Assign AudioSource.outputAudioMixerGroup");
                    src.outputAudioMixerGroup = group;
                    EditorUtility.SetDirty(src);
                    count++;
                }
            }
        }
        Debug.Log($"DiceAudioTools: Assigned mixer group to {managers.Length} DiceManager(s) and {count} AudioSource(s).");
    }
}

