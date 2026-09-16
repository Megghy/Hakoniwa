param([Parameter(Mandatory)][string]$Path)
$bytes = [IO.File]::ReadAllBytes($Path)
$pe = [BitConverter]::ToInt32($bytes, 0x3C)
$offset = $pe + 22
$c = [BitConverter]::ToUInt16($bytes, $offset)
if (($c -band 0x20) -eq 0) {
    [BitConverter]::GetBytes([uint16]($c -bor 0x20)).CopyTo($bytes, $offset)
    [IO.File]::WriteAllBytes($Path, $bytes)
}
