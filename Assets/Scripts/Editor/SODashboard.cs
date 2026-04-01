using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Category5.Items;
using Category5.Player;
using Category5.Boss;
using Category5.Enemies;
using Category5.Core;

namespace Category5.Editor
{
    // scriptable object dashboard - shows all assets of a chosen so type in a sortable inline-editable table
    // open via Category5 -> SO Dashboard
    public class SODashboard : EditorWindow
    {
        // tab/type definitions

        enum SOType { ItemData, PlayerClass, AbilityData, BossAttackData, EnemyData, ProjectileData, SoundData }

        static readonly string[] TypeLabels =
        {
            "ItemData", "PlayerClass", "AbilityData", "BossAttackData", "EnemyData", "ProjectileData", "SoundData"
        };

        // state

        SOType _selectedType = SOType.ItemData;
        int _sortColumn = 0;
        bool _sortAscending = true;
        Vector2 _scroll;

        List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        List<SerializedObject> _serializedObjects = new List<SerializedObject>();

        // prefs keys
        const string PrefKey_Type   = "SODashboard_Type";
        const string PrefKey_ScrollX = "SODashboard_ScrollX";
        const string PrefKey_ScrollY = "SODashboard_ScrollY";

        // row layout
        const float RowHeight   = 18f;
        const float HeaderHeight = 20f;

        // cached styles
        GUIStyle _rowEven;
        GUIStyle _rowOdd;
        GUIStyle _headerButton;
        GUIStyle _cellLabel;
        GUIStyle _pingButton;
        bool _stylesBuilt;

        // column descriptors

        struct ColumnDef
        {
            public string label;
            public float width;
            public bool editable;        // false = read-only label drawn from a lambda
            public string propName;      // serialized property name
            public Func<UnityEngine.Object, string> getDisplayValue; // used for sorting computed cols
        }

        // per-type column tables
        static readonly ColumnDef[] ItemDataColumns =
        {
            new ColumnDef { label = "Name",         width = 130, editable = true,  propName = "itemName"        },
            new ColumnDef { label = "Category",     width = 90,  editable = true,  propName = "category"        },
            new ColumnDef { label = "Gold Cost",    width = 65,  editable = true,  propName = "goldCost"        },
            new ColumnDef { label = "Effects",      width = 52,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("effects"); return p != null ? p.arraySize.ToString() : "0"; }},
            new ColumnDef { label = "Glow Color",   width = 80,  editable = true,  propName = "glowColor"       },
        };

        static readonly ColumnDef[] PlayerClassColumns =
        {
            new ColumnDef { label = "Name",      width = 110, editable = true,  propName = "className"          },
            new ColumnDef { label = "ID",        width = 90,  editable = true,  propName = "classId"            },
            new ColumnDef { label = "Combat",    width = 60,  editable = true,  propName = "combatClass"        },
            new ColumnDef { label = "Base DMG",  width = 65,  editable = true,  propName = "baseAttackDamage"   },
            new ColumnDef { label = "Max HP",    width = 55,  editable = true,  propName = "baseMaxHealth"      },
            new ColumnDef { label = "Mana",      width = 50,  editable = true,  propName = "baseMaxMana"        },
            new ColumnDef { label = "Move Spd",  width = 60,  editable = true,  propName = "baseMoveSpeed"      },
            new ColumnDef { label = "Atk Spd",   width = 60,  editable = true,  propName = "baseAttackSpeed"    },
            new ColumnDef { label = "Crit%",     width = 50,  editable = true,  propName = "baseCritChance"     },
        };

        static readonly ColumnDef[] AbilityDataColumns =
        {
            new ColumnDef { label = "Name",        width = 170, editable = true,  propName = "abilityName"        },
            new ColumnDef { label = "Cooldown",    width = 65,  editable = true,  propName = "cooldownDuration"   },
            new ColumnDef { label = "DMG Coeff",   width = 68,  editable = true,  propName = "damageCoefficient"  },
            new ColumnDef { label = "Mana Cost",   width = 65,  editable = true,  propName = "manaCost"           },
            new ColumnDef { label = "Cast Time",   width = 65,  editable = true,  propName = "castTime"           },
            new ColumnDef { label = "VFX",         width = 38,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("vfxPrefab"); return (p != null && p.objectReferenceValue != null) ? "✓" : "✗"; }},
            new ColumnDef { label = "SFX",         width = 38,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("sfxClip"); return (p != null && p.objectReferenceValue != null) ? "✓" : "✗"; }},
        };

        static readonly ColumnDef[] BossAttackColumns =
        {
            new ColumnDef { label = "Name",       width = 130, editable = true,  propName = "attackName"         },
            new ColumnDef { label = "Type",        width = 80,  editable = true,  propName = "attackType"         },
            new ColumnDef { label = "Weight",      width = 55,  editable = true,  propName = "selectionWeight"    },
            new ColumnDef { label = "Damage",      width = 55,  editable = true,  propName = "damage"             },
            new ColumnDef { label = "HP Thresh",   width = 70,  editable = true,  propName = "healthThreshold"    },
            new ColumnDef { label = "Telegraph",   width = 68,  editable = true,  propName = "telegraphDuration"  },
            new ColumnDef { label = "Lunge",       width = 44,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("hasLunge"); return (p != null && p.boolValue) ? "✓" : "✗"; }},
            new ColumnDef { label = "Sweep",       width = 44,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("isSweep"); return (p != null && p.boolValue) ? "✓" : "✗"; }},
            new ColumnDef { label = "Proj",        width = 40,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("hasProjectile"); return (p != null && p.boolValue) ? "✓" : "✗"; }},
        };

        static readonly ColumnDef[] EnemyDataColumns =
        {
            new ColumnDef { label = "Name",       width = 120, editable = true,  propName = "enemyName"       },
            new ColumnDef { label = "Element",    width = 80,  editable = true,  propName = "elementType"     },
            new ColumnDef { label = "Max HP",     width = 55,  editable = true,  propName = "maxHealth"       },
            new ColumnDef { label = "Move Spd",   width = 65,  editable = true,  propName = "moveSpeed"       },
            new ColumnDef { label = "Damage",     width = 55,  editable = true,  propName = "damage"          },
            new ColumnDef { label = "Atk Range",  width = 65,  editable = true,  propName = "attackRange"     },
            new ColumnDef { label = "Detection",  width = 65,  editable = true,  propName = "detectionRange"  },
        };

        static readonly ColumnDef[] ProjectileDataColumns =
        {
            new ColumnDef { label = "Name",          width = 150, editable = false, propName = null,
                            getDisplayValue = o => o.name },
            new ColumnDef { label = "Speed",         width = 60,  editable = true,  propName = "speed"                       },
            new ColumnDef { label = "DMG Coeff",     width = 68,  editable = true,  propName = "damageCoefficient"           },
            new ColumnDef { label = "Lifetime",      width = 60,  editable = true,  propName = "lifetime"                    },
            new ColumnDef { label = "Allow Charge",  width = 80,  editable = true,  propName = "allowCharge"                 },
            new ColumnDef { label = "Max Charge",    width = 70,  editable = true,  propName = "maxChargeTime"               },
        };

        static readonly ColumnDef[] SoundDataColumns =
        {
            new ColumnDef { label = "Name",       width = 160, editable = false, propName = null,
                            getDisplayValue = o => o.name },
            new ColumnDef { label = "Clips",      width = 42,  editable = false, propName = null,
                            getDisplayValue = o => { var so = new SerializedObject(o); var p = so.FindProperty("clips"); return p != null ? p.arraySize.ToString() : "0"; }},
            new ColumnDef { label = "Volume",     width = 55,  editable = true,  propName = "volume"          },
            new ColumnDef { label = "Pitch",      width = 50,  editable = true,  propName = "pitch"           },
            new ColumnDef { label = "Is 3D",      width = 45,  editable = true,  propName = "is3D"            },
            new ColumnDef { label = "Loop",       width = 40,  editable = true,  propName = "loop"            },
        };

        ColumnDef[] GetColumns(SOType t) => t switch
        {
            SOType.ItemData       => ItemDataColumns,
            SOType.PlayerClass    => PlayerClassColumns,
            SOType.AbilityData    => AbilityDataColumns,
            SOType.BossAttackData => BossAttackColumns,
            SOType.EnemyData      => EnemyDataColumns,
            SOType.ProjectileData => ProjectileDataColumns,
            SOType.SoundData      => SoundDataColumns,
            _                     => Array.Empty<ColumnDef>()
        };

        // open

        [MenuItem("Category5/SO Dashboard")]
        static void Open() => GetWindow<SODashboard>("SO Dashboard");

        // lifecycle

        void OnEnable()
        {
            _selectedType   = (SOType)EditorPrefs.GetInt(PrefKey_Type, 0);
            _scroll.x       = EditorPrefs.GetFloat(PrefKey_ScrollX, 0);
            _scroll.y       = EditorPrefs.GetFloat(PrefKey_ScrollY, 0);
            EditorApplication.projectChanged += RefreshAssets;
            RefreshAssets();
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshAssets;
            EditorPrefs.SetInt(PrefKey_Type, (int)_selectedType);
            EditorPrefs.SetFloat(PrefKey_ScrollX, _scroll.x);
            EditorPrefs.SetFloat(PrefKey_ScrollY, _scroll.y);
            DisposeSerializedObjects();
        }

        void DisposeSerializedObjects()
        {
            foreach (var so in _serializedObjects) so?.Dispose();
            _serializedObjects.Clear();
        }

        // asset loading

        void RefreshAssets()
        {
            DisposeSerializedObjects();
            _assets.Clear();
            _sortColumn = 0;
            _sortAscending = true;

            string typeName = _selectedType switch
            {
                SOType.ItemData       => "ItemData",
                SOType.PlayerClass    => "PlayerClass",
                SOType.AbilityData    => "AbilityData",
                SOType.BossAttackData => "BossAttackData",
                SOType.EnemyData      => "EnemyData",
                SOType.ProjectileData => "ProjectileData",
                SOType.SoundData      => "SoundData",
                _                     => ""
            };

            var guids = AssetDatabase.FindAssets($"t:{typeName}", new[] { "Assets/Data" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null) continue;
                _assets.Add(asset);
                _serializedObjects.Add(new SerializedObject(asset));
            }

            ApplySort();
            UpdateTitle();
            Repaint();
        }

        void UpdateTitle()
        {
            titleContent = new GUIContent($"SO Dashboard — {_selectedType} ({_assets.Count})");
        }

        // sorting

        void ApplySort()
        {
            if (_assets.Count == 0) return;
            var cols = GetColumns(_selectedType);
            if (_sortColumn >= cols.Length) return;
            var col = cols[_sortColumn];

            // build parallel sort keys
            var pairs = new List<(UnityEngine.Object asset, SerializedObject so, string key)>();
            for (int i = 0; i < _assets.Count; i++)
            {
                string key = GetSortKey(_assets[i], _serializedObjects[i], col);
                pairs.Add((_assets[i], _serializedObjects[i], key));
            }

            pairs.Sort((a, b) =>
            {
                // try numeric first
                bool aNum = float.TryParse(a.key, out float af);
                bool bNum = float.TryParse(b.key, out float bf);
                int cmp = (aNum && bNum) ? af.CompareTo(bf) : string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase);
                return _sortAscending ? cmp : -cmp;
            });

            _assets.Clear();
            _serializedObjects.Clear();
            foreach (var (asset, so, _) in pairs)
            {
                _assets.Add(asset);
                _serializedObjects.Add(so);
            }
        }

        string GetSortKey(UnityEngine.Object asset, SerializedObject so, ColumnDef col)
        {
            if (!col.editable && col.getDisplayValue != null)
                return col.getDisplayValue(asset);

            if (col.propName == null) return asset.name;

            var prop = so.FindProperty(col.propName);
            if (prop == null) return "";
            return prop.propertyType switch
            {
                SerializedPropertyType.String  => prop.stringValue,
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Float   => prop.floatValue.ToString("F4"),
                SerializedPropertyType.Boolean => prop.boolValue ? "1" : "0",
                SerializedPropertyType.Enum    => prop.enumDisplayNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                                                    ? prop.enumDisplayNames[prop.enumValueIndex]
                                                    : prop.enumValueIndex.ToString(),
                _ => ""
            };
        }

        void SetSort(int colIndex)
        {
            if (_sortColumn == colIndex)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = colIndex;
                _sortAscending = true;
            }
            ApplySort();
        }

        // styles

        void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _rowEven = new GUIStyle(GUIStyle.none);
            _rowEven.normal.background = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.22f, 1f));

            _rowOdd = new GUIStyle(GUIStyle.none);
            _rowOdd.normal.background = MakeTex(1, 1, new Color(0.19f, 0.19f, 0.19f, 1f));

            _headerButton = new GUIStyle(EditorStyles.toolbarButton);
            _headerButton.alignment = TextAnchor.MiddleLeft;
            _headerButton.fontStyle = FontStyle.Bold;
            _headerButton.fontSize = 11;

            _cellLabel = new GUIStyle(EditorStyles.label);
            _cellLabel.alignment = TextAnchor.MiddleLeft;
            _cellLabel.fontSize = 11;

            _pingButton = new GUIStyle(EditorStyles.miniButton);
            _pingButton.padding = new RectOffset(2, 2, 1, 1);
        }

        static Texture2D MakeTex(int w, int h, Color col)
        {
            var t = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            t.SetPixels(pixels);
            t.Apply();
            return t;
        }

        // GUI

        void OnGUI()
        {
            BuildStyles();

            DrawToolbar();
            DrawTableHeaders();
            DrawTableBody();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            for (int i = 0; i < TypeLabels.Length; i++)
            {
                bool selected = (int)_selectedType == i;
                bool clicked = GUILayout.Toggle(selected, TypeLabels[i], EditorStyles.toolbarButton);
                if (clicked && !selected)
                {
                    _selectedType = (SOType)i;
                    _sortColumn = 0;
                    _sortAscending = true;
                    RefreshAssets();
                    GUIUtility.keyboardControl = 0;
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RefreshAssets();

            EditorGUILayout.EndHorizontal();
        }

        void DrawTableHeaders()
        {
            var cols = GetColumns(_selectedType);
            float totalColWidth = GetTotalColumnWidth(cols);

            // ping button column + cols
            float pingColWidth = 22f;
            Rect headerStrip = GUILayoutUtility.GetRect(0, HeaderHeight, GUILayout.ExpandWidth(true));

            // horizontal scrollbar sync — offset by scroll.x
            float xOffset = headerStrip.x - _scroll.x;

            // ping placeholder header
            Rect pingHeader = new Rect(xOffset, headerStrip.y, pingColWidth, HeaderHeight);
            GUI.Box(pingHeader, GUIContent.none, EditorStyles.toolbar);
            xOffset += pingColWidth;

            for (int i = 0; i < cols.Length; i++)
            {
                Rect r = new Rect(xOffset, headerStrip.y, cols[i].width, HeaderHeight);
                string arrow = (i == _sortColumn) ? (_sortAscending ? " ▲" : " ▼") : "";
                if (GUI.Button(r, cols[i].label + arrow, _headerButton))
                    SetSort(i);
                xOffset += cols[i].width;
            }
        }

        void DrawTableBody()
        {
            var cols = GetColumns(_selectedType);
            float pingColWidth = 22f;
            float totalWidth = pingColWidth + GetTotalColumnWidth(cols);

            if (_assets.Count == 0)
            {
                GUILayout.Space(8);
                EditorGUILayout.LabelField($"No {_selectedType} assets found in Assets/Data/", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // scroll view
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _assets.Count; i++)
            {
                var asset = _assets[i];
                var so    = _serializedObjects[i];

                if (asset == null || so == null) continue;

                GUIStyle rowStyle = (i % 2 == 0) ? _rowEven : _rowOdd;

                Rect rowRect = GUILayoutUtility.GetRect(totalWidth, RowHeight, GUILayout.ExpandWidth(false));
                GUI.Box(rowRect, GUIContent.none, rowStyle);

                float x = rowRect.x;

                // ping button
                Rect pingRect = new Rect(x, rowRect.y + 1, pingColWidth - 2, RowHeight - 2);
                if (GUI.Button(pingRect, "→", _pingButton))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                x += pingColWidth;

                // each column
                so.Update();
                bool modified = false;

                for (int c = 0; c < cols.Length; c++)
                {
                    Rect cellRect = new Rect(x, rowRect.y, cols[c].width - 2, RowHeight);
                    bool changed = DrawCell(cellRect, asset, so, cols[c]);
                    if (changed) modified = true;
                    x += cols[c].width;
                }

                if (modified)
                {
                    so.ApplyModifiedProperties();
                    // re-sort if sorted column changed
                    ApplySort();
                    Repaint();
                    break; // restart loop after re-sort to avoid index drift
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // returns true if the value was changed
        bool DrawCell(Rect rect, UnityEngine.Object asset, SerializedObject so, ColumnDef col)
        {
            // computed / read-only
            if (!col.editable || col.propName == null)
            {
                string val = col.getDisplayValue != null ? col.getDisplayValue(asset) : "";
                GUI.Label(rect, val, _cellLabel);
                return false;
            }

            var prop = so.FindProperty(col.propName);
            if (prop == null)
            {
                GUI.Label(rect, "?", _cellLabel);
                return false;
            }

            EditorGUI.BeginChangeCheck();

            switch (prop.propertyType)
            {
                case SerializedPropertyType.String:
                    prop.stringValue = EditorGUI.TextField(rect, prop.stringValue);
                    break;

                case SerializedPropertyType.Integer:
                    prop.intValue = EditorGUI.IntField(rect, prop.intValue);
                    break;

                case SerializedPropertyType.Float:
                    prop.floatValue = EditorGUI.FloatField(rect, prop.floatValue);
                    break;

                case SerializedPropertyType.Boolean:
                    // draw centered toggle
                    Rect toggleRect = new Rect(rect.x + rect.width * 0.5f - 8, rect.y + 1, 16, RowHeight - 2);
                    prop.boolValue = EditorGUI.Toggle(toggleRect, prop.boolValue);
                    break;

                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = EditorGUI.Popup(rect, prop.enumValueIndex, prop.enumDisplayNames);
                    break;

                case SerializedPropertyType.Color:
                    prop.colorValue = EditorGUI.ColorField(rect, GUIContent.none, prop.colorValue, false, true, false);
                    break;

                default:
                    GUI.Label(rect, prop.propertyType.ToString(), _cellLabel);
                    break;
            }

            return EditorGUI.EndChangeCheck();
        }

        float GetTotalColumnWidth(ColumnDef[] cols)
        {
            float w = 0;
            foreach (var c in cols) w += c.width;
            return w;
        }
    }
}
