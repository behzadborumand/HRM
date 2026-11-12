param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)]$AdminSession,
    [Parameter(Mandatory=$true)]$OwnerSession,
    [Parameter(Mandatory=$true)]$ReaderSession,
    [Parameter(Mandatory=$true)][int]$EmployeeId,
    [Parameter(Mandatory=$true)][int]$PeerId
)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost'){throw 'Local trial host only.'}
function Token([string]$html){$tag=[regex]::Match($html,'<input[^>]*name="__RequestVerificationToken"[^>]*>');return [Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]+)"').Groups[1].Value)}
function Field([string]$html,[string]$name){return [Net.WebUtility]::HtmlDecode([regex]::Match($html,('<input[^>]*name="'+[regex]::Escape($name)+'"[^>]*value="([^"]*)"')).Groups[1].Value)}
function Get([string]$path,$session){Invoke-WebRequest ($BaseUrl+$path) -WebSession $session -UseBasicParsing}
function Post([string]$path,[hashtable]$body,$session){Invoke-WebRequest ($BaseUrl+$path) -Method Post -Body $body -WebSession $session -UseBasicParsing}
$path="/Employees/$EmployeeId/Personal"
$adminPage=Get $path $AdminSession
if($adminPage.Content -notmatch 'data-personal-contact' -or $adminPage.Content -notmatch 'data-personal-identity'){throw 'Admin personal forms missing.'}
$bad=Post ($path+'?handler=Identity') @{'Identity.DateOfBirthJalali'='bad';'Identity.Version'='';__RequestVerificationToken=(Token $adminPage.Content)} $AdminSession
if($bad.Content -notmatch 'validation-summary-errors'){throw 'Invalid Jalali birth date was accepted.'}
$identity=Post ($path+'?handler=Identity') @{'Identity.Phone'='021-12345678';'Identity.DateOfBirthJalali'='1370/01/02';'Identity.Gender'='Female';'Identity.MaritalStatus'='Single';'Identity.Version'='';__RequestVerificationToken=(Token $adminPage.Content)} $AdminSession
if($identity.BaseResponse.ResponseUri.AbsolutePath -ne "/Employees/$EmployeeId" -or $identity.Content -notmatch 'data-private-personal'){throw 'Admin identity save failed.'}
$ownerPage=Get ("/My/Personal?EmployeeId=$PeerId&CompanyId=2147483647") $OwnerSession
if($ownerPage.BaseResponse.ResponseUri.AbsolutePath -ne $path -or $ownerPage.Content -match 'data-personal-identity|name="Identity.DateOfBirthJalali"'){throw 'Owner personal target or identity controls are unsafe.'}
$version=Field $ownerPage.Content 'Contact.Version';$token=Token $ownerPage.Content
$contactBody=@{'Contact.PreferredName'='PRIVATE_PERSONAL_SMOKE';'Contact.MobilePhone'='۰۹۰۰۱۲۳۴۵۶۷';'Contact.AddressLine1'='PRIVATE_ADDRESS_SMOKE';'Contact.AddressLine2'='Unit 2';'Contact.City'='PRIVATE_CITY_SMOKE';'Contact.State'='Province';'Contact.PostalCode'='۱۲۳۴۵۶۷۸۹۰';'Contact.Country'='Iran';'Contact.EmergencyContactName'='PRIVATE_EMERGENCY_SMOKE';'Contact.EmergencyContactPhone'='+98 900 9999999';'Contact.EmergencyContactRelation'='Sibling';'Contact.Version'=$version;
'Identity.DateOfBirthJalali'='1400/01/01';'Identity.Gender'='Male';EmployeeId=$PeerId;CompanyId='2147483647';__RequestVerificationToken=$token}
$saved=Post ($path+'?handler=Contact') $contactBody $OwnerSession
if($saved.BaseResponse.ResponseUri.AbsolutePath -ne "/Employees/$EmployeeId" -or $saved.Content -notmatch 'PRIVATE_ADDRESS_SMOKE' -or $saved.Content -notmatch '09001234567'){throw 'Owner contact save failed.'}
$check=Get $path $AdminSession
if((Field $check.Content 'Identity.DateOfBirthJalali') -ne '1370/01/02' -or $check.Content -notmatch '<option selected="selected" value="Female">|<option value="Female" selected="selected">'){throw 'Contact payload overwrote admin identity fields.'}
$stale=Post ($path+'?handler=Contact') $contactBody $OwnerSession
if($stale.Content -notmatch 'validation-summary-errors'){throw 'Stale personal version succeeded.'}
$ownerPage=Get $path $OwnerSession
$current=Field $ownerPage.Content 'Contact.Version'
$invalidBody=$contactBody.Clone();$invalidBody['Contact.Version']=$current;$invalidBody['Contact.MobilePhone']='not a phone'
$invalid=Post ($path+'?handler=Contact') $invalidBody $OwnerSession
if($invalid.Content -notmatch 'validation-summary-errors'){throw 'Invalid phone accepted.'}
$identityForged=Post ($path+'?handler=Identity') @{'Identity.DateOfBirthJalali'='1400/01/01';'Identity.Version'=$current;__RequestVerificationToken=$token} $OwnerSession
if($identityForged.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Owner changed admin identity.'}
foreach($session in @($OwnerSession,$ReaderSession)){
    $target=if($session -eq $OwnerSession){"/Employees/$PeerId/Personal"}else{$path}
    if((Get $target $session).BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Peer or general reader accessed private personal form.'}
    $forbiddenBody=$contactBody.Clone();$forbiddenBody['__RequestVerificationToken']=(Token (Get '/' $session).Content)
    $denied=Post ($target+'?handler=Contact') $forbiddenBody $session
    if($denied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Peer or general reader changed private data.'}
}
$readerProfile=Get "/Employees/$EmployeeId" $ReaderSession
if($readerProfile.Content -match 'data-private-personal|PRIVATE_PERSONAL_SMOKE|PRIVATE_ADDRESS_SMOKE|PRIVATE_EMERGENCY_SMOKE'){throw 'Private personal values leaked to reader HTML.'}
$noCsrf=$contactBody.Clone();$noCsrf.Remove('__RequestVerificationToken');$noCsrf['Contact.Version']=$current
try{Post ($path+'?handler=Contact') $noCsrf $OwnerSession|Out-Null;throw 'Missing CSRF accepted.'}catch [Net.WebException]{if([int]$_.Exception.Response.StatusCode -ne 400){throw}}
Write-Output 'Private personal forms, owner contact save, admin identity, crafted fields, stale/invalid/CSRF rejection and reader privacy: PASS'
