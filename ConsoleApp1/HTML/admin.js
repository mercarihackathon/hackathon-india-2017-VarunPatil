var markersNow = {};
var markersArray = [];
var markersJson = {};

var map, infoWindow;
function initMap() {
  map = new google.maps.Map(document.getElementById('map'), {
    center: {lat: -34.397, lng: 150.644},
    zoom: 6
  });
  infoWindow = new google.maps.InfoWindow;

  // Try HTML5 geolocation.
  if (navigator.geolocation) {
    navigator.geolocation.getCurrentPosition(function(position) {
      var pos = {
        lat: position.coords.latitude,
        lng: position.coords.longitude
      };

      infoWindow.setPosition(pos);
      infoWindow.setContent('<b>You are here</b>');
      infoWindow.open(map);
      map.setCenter(pos);
	  map.setZoom(17);
	  //alert(pos);
    }, function() {
      handleLocationError(true, infoWindow, map.getCenter());
    });
  } else {
    // Browser doesn't support Geolocation
    handleLocationError(false, infoWindow, map.getCenter());
  }
  
	map.addListener('idle', function(evt) {
		updateMarkers()
	});
  
}

function clearOverlays() {
  for (var i = 0; i < markersArray.length; i++ ) {
    markersArray[i].setMap(null);
  }
  markersArray.length = 0;
}

function handleLocationError(browserHasGeolocation, infoWindow, pos) {
  infoWindow.setPosition(pos);
  infoWindow.setContent(browserHasGeolocation ?
                        'Error: The Geolocation service failed.' :
                        'Error: Your browser doesn\'t support geolocation.');
  infoWindow.open(map);
}

function addmarker(latilongi, key, color) {
	var infowindow = new google.maps.InfoWindow({
		content: "ID: " + key + "<br> <b><large><a href=\"#\">Book this ride!</a><large></b>" 
	});

    var marker = new google.maps.Marker({
        position: latilongi,
        title: 'Bike',
        draggable: false,
        map: map,
		vid: key,
		icon: 'http://maps.google.com/mapfiles/ms/icons/' + color + '-dot.png'
    });
	markersArray.push(marker);
	markersJson[key] = marker;

	google.maps.event.addListener(marker,"click",function(){
		infowindow.open(map, marker);
	});
}

function updateMarkers () {
	currloc = map.getCenter();
	locstr =  "?lat=" + currloc.lat().toFixed(4) + "&lng=" + currloc.lng().toFixed(4);
	$.get("getdata/admin/get" + locstr, function(data, status){
		var obj = JSON.parse(data);
		//clearOverlays();
		for (key in obj) {
			if(markersNow.hasOwnProperty(key)) 
				if (markersNow[key] == obj[key]) continue;			
				else markersJson[key].setMap(null);
			markersNow[key] = obj[key];
			loc = obj[key].split(",");
			
			if (loc[2] == "0") color = 'green';
			else if (loc[2] == "1") color = 'blue';
			else if (loc[2] == "2") color = 'red';
			
			addmarker(new google.maps.LatLng(loc[0], loc[1]), key, color);
		}
		for (key in markersNow) {
			if(!obj.hasOwnProperty(key)) {
				markersJson[key].setMap(null);
				delete markersJson[key];
				delete markersNow[key];
			}
		}
	});
}

window.setInterval(function(){
  updateMarkers();
}, 1000);

$(document).ready(function(){
	$("#go").click(function(){
	    $.get("updateloc?id=1&lat=19.103684&lng=72.871368&unused=1", function(data, status){});
		$.get("updateloc?id=2&lat=19.103884&lng=72.871668&unused=1", function(data, status){});
		$.get("updateloc?id=3&lat=19.103284&lng=72.871468&unused=1", function(data, status){});
	});
});