// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox
{
    /// <summary>
    /// Builds the <c>-fw_cfg</c> arguments used to deliver Guest Settings to a Proxmox vm.
    /// </summary>
    /// <remarks>
    /// The generated text is stored in the vm's <c>args</c> property, and reaches the guest only
    /// after passing through two independent parsers that must each be escaped for:
    /// <list type="number">
    /// <item>
    /// Proxmox splits <c>args</c> into argv with <c>PVE::Tools::split_args</c>, which is
    /// <c>Text::ParseWords::shellwords</c>. It honours quotes and then strips them.
    /// </item>
    /// <item>
    /// QEMU parses one argv element with <c>QemuOpts</c>, which splits on commas. It has no quote
    /// handling at all, so quoting cannot protect a comma; its only escape is a doubled comma,
    /// which it collapses again before the guest reads the value.
    /// </item>
    /// </list>
    /// Because the layers do not meet, both escapes are always required.
    /// </remarks>
    internal static class ProxmoxFwCfg
    {
        /// <summary>
        /// Builds one complete, fully escaped <c>-fw_cfg</c> argument for a Guest Setting.
        /// </summary>
        internal static string Arg(string key, string value)
            => $"-fw_cfg {ShellQuote($"name=opt/{Opts(key)},string={Opts(value)}")}";

        /// <summary>
        /// Escapes for QEMU's QemuOpts parser by doubling every literal comma. QEMU collapses the
        /// doubling before the guest reads the value, so it is invisible to guests and lab authors.
        /// Carriage returns and line feeds are folded to a space because a newline would corrupt
        /// the Proxmox vm config file.
        /// </summary>
        private static string Opts(string value)
            => (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace(",", ",,");

        /// <summary>
        /// Escapes for Proxmox's shellwords parser. Single quotes are used rather than double
        /// quotes because they preserve backslashes and are unaffected by a double quote in the
        /// value. An embedded single quote closes, escapes, and reopens the quoted run.
        /// </summary>
        private static string ShellQuote(string value)
            => $"'{value.Replace("'", @"'\''")}'";
    }
}
