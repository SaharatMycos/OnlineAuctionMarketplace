<#
.SYNOPSIS
  Seeds demo data through the public API (dev auth must be on): the mockup's Chicago-area sellers and
  listings, a few far-away items that ship, and some opening bids.
.DESCRIPTION
  Uses only HTTP endpoints, exactly like a client would, so integration events, the Search read model and
  auctions are all created the normal way. Safe to re-run: it just adds more listings.
.EXAMPLE
  pwsh -File scripts/seed-demo.ps1
  pwsh -File scripts/seed-demo.ps1 -ApiUrl http://localhost:5080 -ShortAuctions
#>
param(
  [string]$ApiUrl = 'http://localhost:5080',
  # Make every auction end in 5-15 minutes, handy for trying the close and deal agreement flow.
  [switch]$ShortAuctions
)
$ErrorActionPreference = 'Stop'

function Token([string]$email) {
  (Invoke-RestMethod -Method Post -Uri "$ApiUrl/v1/dev/token" -ContentType 'application/json' -Body (@{ email = $email } | ConvertTo-Json)).token
}

function Publish([string]$token, [hashtable]$listing, [hashtable]$location) {
  $body = @{ listing = $listing; location = $location; publish = $true } | ConvertTo-Json -Depth 5
  Invoke-RestMethod -Method Post -Uri "$ApiUrl/v1/listings" -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body $body
}

function Bid([string]$token, [string]$auctionId, [decimal]$max) {
  $headers = @{ Authorization = "Bearer $token"; 'Idempotency-Key' = [guid]::NewGuid().ToString() }
  Invoke-RestMethod -Method Post -Uri "$ApiUrl/v1/auctions/$auctionId/bids" -Headers $headers -ContentType 'application/json' -Body (@{ maxAmount = $max } | ConvertTo-Json) | Out-Null
}

function AuctionFor([string]$listingId) {
  for ($i = 0; $i -lt 20; $i++) {
    try { return Invoke-RestMethod -Uri "$ApiUrl/v1/auctions/by-listing/$listingId" } catch { Start-Sleep -Milliseconds 300 }
  }
  throw "No auction for listing $listingId (is the worker running?)"
}

$duration = { param($normal) if ($ShortAuctions) { 5 } else { $normal } }

# Seller, listing, exact location (private; the API only ever shows a ~1 km area).
$items = @(
  @{ seller = 'dan_rides'; lat = 41.9088; lng = -87.6776; address = '1550 N Damen Ave'; listing = @{
      categoryId = 9; title = 'Trek FX 3 Disc hybrid bike (2022), size M'; condition = 'Used, good'; delivery = 'pickup'
      description = 'Carbon fork, hydraulic disc brakes. New tyres in spring. Test ride welcome before bidding.'
      specs = @{ Brand = 'Trek'; Model = 'FX 3 Disc'; Size = 'M'; Year = '2022' }; startPrice = 150; durationMinutes = (& $duration 1440) } }
  @{ seller = 'oakpark_moving'; lat = 41.8850; lng = -87.7845; address = '214 S Oak Park Ave'; listing = @{
      categoryId = 4; title = 'IKEA KALLAX 4x4 shelf unit, white (moving sale)'; condition = 'Used, good'; delivery = 'pickup'
      description = 'Already disassembled, all hardware in a bag. Must go by Saturday.'
      specs = @{ Brand = 'IKEA'; Model = 'KALLAX'; Size = '147 x 147 cm' }; startPrice = 10; buyNowPrice = 60; durationMinutes = (& $duration 60) } }
  @{ seller = 'naper_gamer'; lat = 41.7508; lng = -88.1535; address = '1010 W Jefferson Ave, Naperville'; listing = @{
      categoryId = 1; title = 'Nintendo Switch OLED + 3 games (Zelda, Mario Kart, Smash)'; condition = 'Used, excellent'; delivery = 'both'; shippingCost = 12
      description = 'Screen protector since day one. Pickup in Naperville or I can ship.'
      specs = @{ Model = 'OLED (white)'; Storage = '64 GB' }; startPrice = 120; reservePrice = 200; durationMinutes = (& $duration 4320) } }
  @{ seller = 'loop_bistro'; lat = 41.8827; lng = -87.6480; address = '900 W Randolph St (loading dock)'; listing = @{
      categoryId = 7; title = 'Restaurant closing: 30 bistro chairs + 8 marble tables (lot)'; condition = 'Used, commercial grade'; delivery = 'pickup'
      description = 'Full dining-room set from a closing West Loop bistro. Inspection by appointment.'
      specs = @{ Chairs = '30'; Tables = '8 (marble top)' }; startPrice = 600; reservePrice = 1500; durationMinutes = (& $duration 10080) } }
  @{ seller = 'hydepark_fit'; lat = 41.7943; lng = -87.5907; address = '5300 S Lake Park Ave'; listing = @{
      categoryId = 9; title = 'Peloton Bike+ with shoes (size 42) and weights'; condition = 'Used, like new'; delivery = 'pickup'
      description = 'About 40 rides. Subscription not included. Help loading available.'
      specs = @{ Model = 'Bike+'; Extras = 'Shoes, 2 lb weights, mat' }; startPrice = 400; durationMinutes = (& $duration 1440) } }
  @{ seller = 'pdx_cameras'; lat = 45.5265; lng = -122.6812; address = '1200 NW Glisan St, Portland'; listing = @{
      categoryId = 1; title = 'Fujifilm X100V, silver, with case'; condition = 'Used, excellent'; delivery = 'both'; shippingCost = 15
      description = 'Shutter count around 4k. Original box and two batteries.'
      specs = @{ Brand = 'Fujifilm'; Model = 'X100V' }; startPrice = 800; buyNowPrice = 1350; durationMinutes = (& $duration 7200) } }
  @{ seller = 'dallas_cards'; lat = 32.7831; lng = -96.8067; address = '2500 Elm St, Dallas'; listing = @{
      categoryId = 2; title = '1999 Pokemon Base Set Charizard, PSA 7'; condition = 'Graded'; delivery = 'ship'; shippingCost = 8
      description = 'Ships insured and tracked.'; specs = @{ Grade = 'PSA 7' }; startPrice = 300; durationMinutes = (& $duration 4320) } }
)

$auctions = @{}
foreach ($item in $items) {
  $token = Token "$($item.seller)@example.test"
  $listing = Publish $token $item.listing @{ lat = $item.lat; lng = $item.lng; address = $item.address }
  $auction = AuctionFor $listing.id
  $auctions[$item.listing.title] = $auction.id
  Write-Output ("Published {0,-60} {1}" -f $listing.title, $listing.areaName)
}

# Some opening bids so the feed looks alive (bob and carol are the demo buyers).
$bob = Token 'bob@example.test'
$carol = Token 'carol@example.test'
Bid $bob $auctions['Trek FX 3 Disc hybrid bike (2022), size M'] 260
Bid $carol $auctions['Trek FX 3 Disc hybrid bike (2022), size M'] 240
Bid $carol $auctions['Nintendo Switch OLED + 3 games (Zelda, Mario Kart, Smash)'] 180
Bid $bob $auctions['Peloton Bike+ with shoes (size 42) and weights'] 520
Write-Output "Placed demo bids. Sign in at http://localhost:3000/signin as Alice, Bob or Carol."
