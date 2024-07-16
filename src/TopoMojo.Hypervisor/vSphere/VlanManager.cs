// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vSphere
{
    public class VlanManager
    {

        public VlanManager (
            VlanConfiguration options
        ) {
            _options = options;
            InitVlans();
        }

        protected VlanConfiguration _options;
        protected Dictionary<string, Vlan> _vlans;
        protected BitArray _vlanMap;

        private void InitVlans()
        {
            //initialize vlan map
            _vlanMap = new BitArray(4096, true);
            foreach (int i in _options.Range.ExpandRange())
            {
                _vlanMap[i] = false;
            }

            //set admin reservations
            _vlans = new Dictionary<string,Vlan>();
            foreach (Vlan vlan in _options.Reservations)
            {
                _vlans.Add(vlan.Name, vlan);
                _vlanMap[vlan.Id] = true;
            }
        }

        public bool Contains(string net)
        {
            return _vlans.ContainsKey(net);
        }

        public void Activate(Vlan[] vlans)
        {
            lock(_vlanMap)
            {
                foreach (Vlan vlan in vlans)
                {
                    if (vlan.OnUplink)
                        _vlanMap[vlan.Id] = true;

                    if (!_vlans.ContainsKey(vlan.Name))
                        _vlans.Add(vlan.Name, vlan);
                }
            }
        }

        public void Deactivate(string net)
        {
            //only deallocate tagged nets
            if (!net.Contains("#"))
                return;

            lock(_vlanMap)
            {
                if (_vlans.ContainsKey(net))
                {
                    if (_vlans[net].OnUplink)
                        _vlanMap[_vlans[net].Id] = false;
                    _vlans.Remove(net);
                }
            }
        }

        public virtual void ReserveVlans(VmTemplate template, bool UseUplinkSwitch)
        {
            lock (_vlanMap)
            {
                foreach (VmNet eth in template.Eth)
                {
                    //if net already reserved, use reserved vlan
                    if (_vlans.ContainsKey(eth.Net))
                    {
                        eth.Vlan = _vlans[eth.Net].Id;
                    }
                    else
                    {
                        int id = 0;
                        if (UseUplinkSwitch)
                        {
                            //get available uplink vlan
                            while (id < _vlanMap.Length && _vlanMap[id])
                            {
                                id += 1;
                            }

                            if (id > 0 && id < _vlanMap.Length)
                            {
                                eth.Vlan = id;
                                _vlanMap[id] = true;
                                _vlans.Add(eth.Net, new Vlan { Name  = eth.Net, Id = id, OnUplink = true });
                            }
                            else
                            {
                                throw new Exception("Unable to reserve a vlan for " + eth.Net);
                            }
                        }
                        else {
                            //get highest vlan in this isolation group
                            id = 100;
                            foreach (string key in _vlans.Keys.Where(k => k.EndsWith(template.IsolationTag)))
                                id = Math.Max(id, _vlans[key].Id);
                            id += 1;
                            eth.Vlan = id;
                            _vlans.Add(eth.Net, new Vlan { Name = eth.Net, Id = id });
                        }

                    }
                }
            }
        }

        public string[] FindNetworks(string tag)
        {
            List<string> nets = new List<string>();
            nets.AddRange(_vlans.Keys.Where(x => !x.Contains("#")));
            nets.AddRange(_vlans.Keys.Where(x => x.Contains(tag)));
            nets.Sort();
            return nets.ToArray();
        }

    }
}
