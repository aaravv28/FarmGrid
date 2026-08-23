document.addEventListener("DOMContentLoaded", function () {

    const searchInput = document.getElementById("productSearch");

    if (searchInput) {

        searchInput.addEventListener("keyup", function () {

            const searchValue =
                this.value.toLowerCase();

            const products =
                document.querySelectorAll(".product-item");

            products.forEach(function (product) {

                const text =
                    product.innerText.toLowerCase();

                product.style.display =
                    text.includes(searchValue)
                        ? ""
                        : "none";

            });

        });

    }


    const addCartButtons =
        document.querySelectorAll(".add-cart-btn");

    addCartButtons.forEach(function (button) {

        button.addEventListener("click", function () {

            const originalText =
                button.innerText;

            button.innerText =
                "Added ✓";

            setTimeout(function () {

                button.innerText =
                    originalText;

            }, 1200);

        });

    });


    const timer =
        document.getElementById("quickTimer");

    if (timer) {

        let remainingSeconds =
            (24 * 60 * 60)
            + (15 * 60)
            + 30;

        setInterval(function () {

            if (remainingSeconds <= 0) {
                return;
            }

            remainingSeconds--;

            const hours =
                Math.floor(
                    remainingSeconds / 3600
                );

            const minutes =
                Math.floor(
                    (remainingSeconds % 3600)
                    / 60
                );

            const seconds =
                remainingSeconds % 60;

            timer.innerText =
                String(hours).padStart(2, "0")
                + ":"
                + String(minutes).padStart(2, "0")
                + ":"
                + String(seconds).padStart(2, "0");

        }, 1000);

    }

});
