using UnityEngine;
using System.Collections.Generic;
using clojure.lang;
using System.Linq;

// for starters, I think we should build a version that deals with a missing
// key someone wants access to by distributing an uninhabited kv pair that falls back on a runtime
// hashmap lookup. Then we can optimize that away once the rest of the logic is confirmed to work. 

namespace Arcadia
{
	public class JumpMap
	{
		// make private!
		public Dictionary<object, KeyVal> dict;


		// ==========================================================
		// constructor 

		public JumpMap ()
		{
			dict = new Dictionary<object, KeyVal>();
		}

		// ==========================================================
		// instance methods

		public KeyVal[] KeyVals ()
		{
			return dict.Values.ToArray();			
		}

		public object ValueAtKey (object k)
		{
			KeyVal val = null;
			if (dict.TryGetValue(k, out val)) {
				// NOT val.GetVal()
				return val.val;
			} else {
				return null;
			}			
		}

		// sadly it seems we will need null keyvals
		// do we need them EVERY time we ask?
		// let us say we do not
		public KeyVal KeyValAtKey (object k)
		{
			KeyVal val = null;
			dict.TryGetValue(k, out val);
			return val;
		}

		// here's a place to optimize later
		public KeyVal Subscribe (object k)
		{
			KeyVal kv = KeyValAtKey(k);
			// not hanging onto this for now (would need GC stuff to prevent memory leak):
			if (kv == null) {
				kv = new KeyVal(k, null, this, false);
			}
			return kv;
		}

		// ----------------------------------------------------------
		// beginnings of System.Collections.IDictionary

		public bool ContainsKey (object k)
		{
			return dict.ContainsKey(k) && KeyValAtKey(k).isInhabited;
		}

		public void Add (object k, object v)
		{
			if (ContainsKey(k)) {
				KeyValAtKey(k).val = v;
			} else {
				KeyVal kv = new KeyVal(k, v, this, true);
				dict.Add(k, kv);
			}
		}

		public void AddAll (clojure.lang.IPersistentMap map)
		{
			foreach (var entry in map) {
				Add(entry.key(), entry.val());
			}
		}

		public void Remove (object k)
		{
			KeyVal kv = KeyValAtKey(k);
			if (kv != null) {
				kv.Evacuate();
				dict.Remove(k);
			}
		}

		// ==========================================================
		// KeyVal

		public class KeyVal
		{
			public readonly object key;
			public object val;

			// here's where we get just terrible
			public bool isInhabited;

			public JumpMap jumpMap;

			public KeyVal (object _key, object _val, JumpMap _jumpMap, bool _isInhabited)
			{
				key = _key;
				val = _val;
				jumpMap = _jumpMap;
				isInhabited = _isInhabited;
			}

			public void Evacuate ()
			{
				// key is probably interned keyword anyway
				isInhabited = false;
				val = null;
			}

			public object GetVal ()
			{
				if (isInhabited) {
					return val;
				} else {
					// fallback to hash lookup
					return this.jumpMap.ValueAtKey(key);
				}
			}

		}

		// ==========================================================
		// View

		public class PartialArrayMapView
		{
			public KeyVal[] kvs;
			public JumpMap source;

			public PartialArrayMapView (object[] keys, JumpMap source_)
			{
				kvs = new KeyVal[keys.Length];
				source = source_;
				for (int i = 0; i < keys.Length; i++) {
					kvs[i] = source.Subscribe(keys[i]);
				}
			}

			public object ValueAtKey (object key)
			{
				for (int i = 0; i < kvs.Length; i++) {
					if (kvs[i].key == key) {
						return kvs[i].GetVal();
					}
				}
				return source.ValueAtKey(key);
			}

			public void Refresh ()
			{
				for (int i = 0; i < kvs.Length; i++) {
					if (!kvs[i].isInhabited && source.ContainsKey(kvs[i].key)) {
						kvs[i] = source.Subscribe(kvs[i].key);
					}
				}
			}

		}

	}
}
